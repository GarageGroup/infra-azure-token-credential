using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GarageGroup.Infra;

internal sealed partial class AccessTokenRemoteCache
{
    private static readonly JsonSerializerOptions SerializerOptions;

    static AccessTokenRemoteCache()
        =>
        SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static AccessTokenRemoteCache? InternalResolveStandard(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<AccessTokenRemoteCache>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var storageOption = ParseStorageOption(configuration, logger);
        if (storageOption is null)
        {
            return null;
        }

        return new(
            option: storageOption,
            httpHandler: serviceProvider.GetRequiredService<ISocketsHttpHandlerProvider>().GetOrCreate(string.Empty),
            logger: logger);
    }

    private const int SasTokenTtlInSeconds = 60;

    private const string SectionKey = "AzureTokenCache";

    private const string DefaultContainerName = "token-cache";

    private const string ContainerResource = "c";

    private const string BlobResource = "b";

    private const string PermissionsList = "l";

    private const string PermissionsRead = "r";

    private const string PermissionsUpload = "w";

    private const string SasVersion = "2022-11-02";

    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ssZ";

    private const int ExpirationPeriodInMinutes = 3;

    private readonly StorageOption option;

    private readonly SocketsHttpHandler httpHandler;

    private readonly ILogger? logger;

    private AccessTokenRemoteCache(StorageOption option, SocketsHttpHandler httpHandler, ILogger<AccessTokenRemoteCache>? logger)
    {
        this.option = option;
        this.httpHandler = httpHandler;
        this.logger = logger;
    }

    private static StorageOption? ParseStorageOption(IConfiguration configuration, ILogger<AccessTokenRemoteCache>? logger)
    {
        var section = configuration.GetSection(SectionKey);
        if (section.GetValue<bool>("Disabled") is true)
        {
            logger?.LogInformation(
                "{SectionKey} configuration is disabled. AccessTokenRemoteCache will not be used.", SectionKey);

            return null;
        }

        var storageConnectionString = section["ConnectionString"];
        if (string.IsNullOrWhiteSpace(storageConnectionString))
        {
            storageConnectionString = configuration["AzureWebJobsStorage"];
            if (string.IsNullOrWhiteSpace(storageConnectionString))
            {
                logger?.LogInformation(
                    "{SectionKey} configuration is not found or empty. AccessTokenRemoteCache will not be used.", SectionKey);

                return null;
            }
        }

        var containerName = section["ContainerName"];
        if (string.IsNullOrWhiteSpace(containerName))
        {
            containerName = DefaultContainerName;
        }

        Dictionary<string, string> connectionItems = new(StringComparer.OrdinalIgnoreCase);
        var parts = storageConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var equalSignIndex = part.IndexOf('=', StringComparison.Ordinal);
            if (equalSignIndex <= 0 || equalSignIndex >= part.Length - 1)
            {
                continue;
            }

            connectionItems[part[..equalSignIndex]] = part[(equalSignIndex + 1)..];
        }

        if (string.Equals(connectionItems.GetValueOrDefault("UseDevelopmentStorage"), "true", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogInformation(
                "Azure Storage emulator connection string is detected for AzureWebJobsStorage. AccessTokenRemoteCache will not be used.");

            return null;
        }

        var endpointsProtocol = connectionItems.GetValueOrDefault("DefaultEndpointsProtocol");
        var accountName = connectionItems.GetValueOrDefault("AccountName");
        var accountKey = connectionItems.GetValueOrDefault("AccountKey");

        if (string.IsNullOrWhiteSpace(endpointsProtocol) || string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(accountKey))
        {
            logger?.LogWarning(
                "AzureWebJobsStorage connection string is missing required items. DefaultEndpointsProtocol: '{EndpointsProtocol}', " +
                "AccountName: '{AccountName}', AccountKey is present: {IsAccountKeyPresent}. AccessTokenRemoteCache will not be used.",
                endpointsProtocol,
                accountName,
                string.IsNullOrWhiteSpace(accountKey) is false);

            return null;
        }

        var endpointSuffix = connectionItems.GetValueOrDefault("EndpointSuffix");
        if (string.IsNullOrWhiteSpace(endpointSuffix))
        {
            endpointSuffix = ResolveEndpointSuffixFromBlobEndpoint(accountName, connectionItems.GetValueOrDefault("BlobEndpoint"));
        }

        if (string.IsNullOrWhiteSpace(endpointSuffix))
        {
            logger?.LogWarning(
                 "Unable to resolve EndpointSuffix for AzureWebJobsStorage connection string. AccessTokenRemoteCache will not be used.");

            return null;
        }

        return new()
        {
            EndpointsProtocol = endpointsProtocol,
            AccountName = accountName,
            AccountKey = accountKey,
            EndpointSuffix = endpointSuffix,
            ContainerName = containerName
        };
    }

    private sealed record class StorageOption
    {
        public required string EndpointsProtocol { get; init; }

        public required string AccountName { get; init; }

        public required string AccountKey { get; init; }

        public required string EndpointSuffix { get; init; }

        public required string ContainerName { get; init; }
    }

    private HttpRequestMessage BuildSharedKeyRequest(HttpMethod method, string requestUri)
    {
        var utcDate = DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture);
        var authorizationHeader = BuildSharedKeyAuthorizationHeader(method, requestUri, utcDate);

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.TryAddWithoutValidation("x-ms-date", utcDate);
        request.Headers.TryAddWithoutValidation("x-ms-version", SasVersion);
        request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);

        return request;
    }

    private string BuildSharedKeyAuthorizationHeader(HttpMethod method, string requestUri, string utcDate)
    {
        var canonicalizedHeaders = $"x-ms-date:{utcDate}\nx-ms-version:{SasVersion}";
        var canonicalizedResource = BuildCanonicalizedResource(requestUri);

        const string empty = "";
        var stringToSign = string.Join('\n',
            method.Method,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            canonicalizedHeaders,
            canonicalizedResource);

        using var hmac = new HMACSHA256(Convert.FromBase64String(option.AccountKey));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        return $"SharedKey {option.AccountName}:{signature}";
    }

    private string BuildCanonicalizedResource(string requestUri)
    {
        var uri = new Uri(
            $"{option.EndpointsProtocol}://{option.AccountName}.blob.{option.EndpointSuffix}{requestUri}",
            UriKind.Absolute);

        var builder = new StringBuilder($"/{option.AccountName}{uri.AbsolutePath}");

        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return builder.ToString();
        }

        var queryPairs = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var queryItem in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var splitIndex = queryItem.IndexOf('=', StringComparison.Ordinal);
            var rawName = splitIndex < 0 ? queryItem : queryItem[..splitIndex];
            var rawValue = splitIndex < 0 ? string.Empty : queryItem[(splitIndex + 1)..];

            var name = Uri.UnescapeDataString(rawName).ToLowerInvariant();
            var value = Uri.UnescapeDataString(rawValue);

            if (queryPairs.TryGetValue(name, out var values) is false)
            {
                values = [];
                queryPairs[name] = values;
            }

            values.Add(value);
        }

        foreach (var pair in queryPairs)
        {
            pair.Value.Sort(StringComparer.Ordinal);
            builder.Append('\n').Append(pair.Key).Append(':').Append(string.Join(',', pair.Value));
        }

        return builder.ToString();
    }

    private HttpClient BuildHttpClient()
        =>
        new(handler: httpHandler, disposeHandler: false)
        {
            BaseAddress = new($"{option.EndpointsProtocol}://{option.AccountName}.blob.{option.EndpointSuffix}/")
        };

    private async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        HttpRequestMessage httpRequest,
        string operationName,
        CancellationToken cancellationToken)
    {
        var method = httpRequest.Method.Method;
        var requestUri = GetRequestUriForLog(httpRequest.RequestUri);

        logger?.LogInformation(
            "AccessTokenRemoteCache HTTP request started: operation '{OperationName}', method '{HttpMethod}', uri '{RequestUri}'",
            operationName,
            method,
            requestUri);

        var stopwatch = Stopwatch.StartNew();

        var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

        logger?.LogInformation(
            "AccessTokenRemoteCache HTTP request completed: operation '{OperationName}', method '{HttpMethod}', " +
            "uri '{RequestUri}', status {HttpStatusCode}, elapsed {ElapsedMilliseconds}ms",
            operationName,
            method,
            requestUri,
            (int)httpResponse.StatusCode,
            stopwatch.ElapsedMilliseconds);

        return httpResponse;
    }

    private string BuildSignedUrl(string permissions, string? fileName = null, string? urlParams = null)
    {
        using var hashAlgorithm = new HMACSHA256(Convert.FromBase64String(option.AccountKey));
        var expiryTime = DateTimeOffset.UtcNow.AddSeconds(SasTokenTtlInSeconds).ToString(DateTimeFormat);

        var resource = string.IsNullOrWhiteSpace(fileName) ? ContainerResource : BlobResource;

        var pathBuilder = new StringBuilder($"/blob/{option.AccountName}/{option.ContainerName}");
        if (string.IsNullOrEmpty(fileName) is false)
        {
            pathBuilder = pathBuilder.Append('/').Append(fileName);
        }

        string[] signParameters =
        [
            permissions,
            string.Empty,
            expiryTime,
            pathBuilder.ToString(),
            string.Empty,
            string.Empty,
            option.EndpointsProtocol,
            SasVersion,
            resource,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        ];

        var urlBuilder = new StringBuilder($"/{option.ContainerName}");
        if (string.IsNullOrEmpty(fileName) is false)
        {
            urlBuilder = urlBuilder.Append('/').Append(fileName);
        }

        var dataToSign = Encoding.UTF8.GetBytes(string.Join('\n', signParameters));
        var signature = Convert.ToBase64String(hashAlgorithm.ComputeHash(dataToSign));

        var escapedSignature = Uri.EscapeDataString(signature);

        urlBuilder = urlBuilder.Append(
            $"?sv={SasVersion}&spr={option.EndpointsProtocol}&se={expiryTime}&sr={resource}&sp={permissions}&sig={escapedSignature}");

        if (string.IsNullOrEmpty(urlParams) is false)
        {
            urlBuilder = urlBuilder.Append('&').Append(urlParams);
        }

        return urlBuilder.ToString();
    }

    private static string ComputeHash(byte[] body)
    {
        var hash = SHA256.HashData(body);
        var builder = new StringBuilder();

        foreach (var hashByte in hash)
        {
            builder = builder.Append(hashByte.ToString("x2"));
        }

        return builder.ToString();
    }

    private static string GetRequestUriForLog(Uri? uri)
    {
        if (uri is null)
        {
            return string.Empty;
        }

        var value = uri.IsAbsoluteUri ? uri.PathAndQuery : uri.OriginalString;
        var queryStartIndex = value.IndexOf('?', StringComparison.Ordinal);
        return queryStartIndex < 0 ? value : value[..queryStartIndex];
    }

    private static string? ResolveEndpointSuffixFromBlobEndpoint(string accountName, string? blobEndpoint)
    {
        if (string.IsNullOrWhiteSpace(blobEndpoint) || Uri.TryCreate(blobEndpoint, UriKind.Absolute, out var uri) is false)
        {
            return null;
        }

        var expectedPrefix = $"{accountName}.blob.";
        if (uri.Host.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return uri.Host[expectedPrefix.Length..];
        }

        return null;
    }

    private static string BuildFileName(TokenRequestContext requestContext)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(TokenRequestContextJson.From(requestContext), SerializerOptions);
        return $"{ComputeHash(json)}.json";
    }

    private static DateTimeOffset GetMinExpirationTime()
        =>
        DateTimeOffset.Now.AddMinutes(ExpirationPeriodInMinutes);

    private sealed record class TokenRequestContextJson
    {
        public string[] Scopes { get; init; } = [];

        public string? ParentRequestId { get; init; }

        public string? Claims { get; init; }

        public string? TenantId { get; init; }

        public bool IsCaeEnabled { get; init; }

        public bool IsProofOfPossessionEnabled { get; init; }

        public string? ProofOfPossessionNonce { get; init; }

        public string? ResourceRequestMethod { get; init; }

        public string? ResourceRequestUri { get; init; }

        public TokenRequestContext ToModel()
            =>
            new(
                scopes: Scopes,
                parentRequestId: ParentRequestId,
                claims: Claims,
                tenantId: TenantId,
                isCaeEnabled: IsCaeEnabled,
                isProofOfPossessionEnabled: IsProofOfPossessionEnabled,
                proofOfPossessionNonce: ProofOfPossessionNonce,
                requestUri: ResolveUriOrNull(ResourceRequestUri),
                requestMethod: ResourceRequestMethod);

        public static TokenRequestContextJson From(TokenRequestContext requestContext)
            =>
            new()
            {
                Scopes = requestContext.Scopes,
                ParentRequestId = requestContext.ParentRequestId,
                Claims = requestContext.Claims,
                TenantId = requestContext.TenantId,
                IsCaeEnabled = requestContext.IsCaeEnabled,
                IsProofOfPossessionEnabled = requestContext.IsProofOfPossessionEnabled,
                ProofOfPossessionNonce = requestContext.ProofOfPossessionNonce,
                ResourceRequestMethod = requestContext.ResourceRequestMethod,
                ResourceRequestUri = requestContext.ResourceRequestUri?.OriginalString
            };

        private static Uri? ResolveUriOrNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri) ? uri : null;
        }
    }

    private sealed record class AccessTokenJson
    {
        public required string Token { get; init; }

        public required DateTimeOffset ExpiresOn { get; init; }

        public DateTimeOffset? RefreshOn { get; init; }

        public string? TokenType { get; init; }

        public AccessToken ToModel()
        {
            if (string.IsNullOrWhiteSpace(TokenType))
            {
                return new(Token, ExpiresOn, RefreshOn);
            }

            return new(Token, ExpiresOn, RefreshOn, TokenType);
        }

        public static AccessTokenJson From(AccessToken accessToken)
            =>
            new()
            {
                Token = accessToken.Token,
                ExpiresOn = accessToken.ExpiresOn,
                RefreshOn = accessToken.RefreshOn,
                TokenType = accessToken.TokenType
            };
    }

    private sealed record class TokenCacheItemJson
    {
        public required TokenRequestContextJson Context { get; init; }

        public required AccessTokenJson AccessToken { get; init; }
    }
}
