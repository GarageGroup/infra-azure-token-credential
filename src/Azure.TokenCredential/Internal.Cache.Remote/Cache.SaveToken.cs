using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace GarageGroup.Infra;

partial class AccessTokenRemoteCache
{
    public async Task SaveTokenAsync(TokenRequestContext requestContext, AccessToken accessToken, CancellationToken cancellationToken)
    {
        using var httpClient = BuildHttpClient();

        var body = JsonSerializer.Serialize(
            new TokenCacheItemJson
            {
                Context = TokenRequestContextJson.From(requestContext),
                AccessToken = AccessTokenJson.From(accessToken)
            },
            SerializerOptions);

        using var httpResponse = await SaveTokenCoreAsync(
            httpClient: httpClient,
            requestContext: requestContext,
            body: body,
            operationName: "SaveToken",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (httpResponse.IsSuccessStatusCode)
        {
            return;
        }

        var failureText = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (httpResponse.StatusCode is not HttpStatusCode.NotFound)
        {
            logger?.LogError(
                "Unable to save access token to remote cache. HTTP status: {HttpStatusCode}. Response: {ResponseText}",
                (int)httpResponse.StatusCode,
                failureText);

            return;
        }

        logger?.LogWarning(
                "Token cache blob save returned 404. " +
                "Container '{ContainerName}' likely does not exist. Trying to create container via SharedKey.",
                ContainerName);

        var isContainerCreated = await EnsureContainerExistsBySharedKeyAsync(cancellationToken).ConfigureAwait(false);
        if (isContainerCreated is false)
        {
            logger?.LogWarning(
                "Unable to ensure token cache container via SharedKey after 404 on save. Save operation will be skipped.");

            return;
        }

        using var retryResponse = await SaveTokenCoreAsync(
            httpClient: httpClient,
            requestContext: requestContext,
            body: body,
            operationName: "SaveTokenRetryAfterContainerCreate",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (retryResponse.IsSuccessStatusCode)
        {
            return;
        }

        var retryFailureText = await retryResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        logger?.LogError(
            "Unable to save access token to remote cache after container creation retry. " +
            "HTTP status: {HttpStatusCode}. Response: {ResponseText}",
            (int)retryResponse.StatusCode,
            retryFailureText);

        return;
    }

    private async ValueTask<HttpResponseMessage> SaveTokenCoreAsync(
        HttpClient httpClient,
        TokenRequestContext requestContext,
        string body,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(
            method: HttpMethod.Put,
            requestUri: BuildSignedUrl(permissions: PermissionsUpload, fileName: BuildFileName(requestContext)));
        
        httpRequest.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
        httpRequest.Headers.TryAddWithoutValidation("x-ms-version", SasVersion);
        httpRequest.Content = new StringContent(body, Encoding.UTF8);
        httpRequest.Content.Headers.ContentType = new("application/json");

        return await SendAsync(
            httpClient: httpClient,
            httpRequest: httpRequest,
            operationName: operationName,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> EnsureContainerExistsBySharedKeyAsync(CancellationToken cancellationToken)
    {
        logger?.LogInformation("Trying to create '{ContainerName}' container using SharedKey authorization.", ContainerName);

        using var httpClient = BuildHttpClient();

        using var createRequest = BuildSharedKeyRequest(
            method: HttpMethod.Put,
            requestUri: $"/{ContainerName}?restype=container");

        using var createResponse = await SendAsync(
            httpClient: httpClient,
            httpRequest: createRequest,
            operationName: "CreateContainerSharedKey",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (createResponse.IsSuccessStatusCode || createResponse.StatusCode is HttpStatusCode.Conflict)
        {
            logger?.LogDebug(
                "AccessTokenRemoteCache fallback has ensured '{ContainerName}' container exists via SharedKey.",
                ContainerName);

            return true;
        }

        var responseText = await createResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        logger?.LogError(
            "AccessTokenRemoteCache fallback failed to create '{ContainerName}' container via SharedKey. " +
            "HTTP status: {HttpStatusCode}. Response: {ResponseText}",
            ContainerName,
            (int)createResponse.StatusCode,
            responseText);

        return false;
    }
}
