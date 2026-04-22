using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace GarageGroup.Infra;

partial class AccessTokenRemoteCache
{
    public async ValueTask<AccessToken?> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        using var httpClient = BuildHttpClient();
        using var httpRequest = new HttpRequestMessage(
            method: HttpMethod.Get,
            requestUri: BuildSignedUrl(permissions: PermissionsRead, fileName: BuildFileName(requestContext)));

        var httpResponse = await SendAsync(
            httpClient: httpClient,
            httpRequest: httpRequest,
            operationName: "GetToken",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (httpResponse.IsSuccessStatusCode is false)
        {
            if (httpResponse.StatusCode is HttpStatusCode.NotFound)
            {
                return null;
            }

            var failureText = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            logger?.LogError(
                "Unable to read access token from remote cache. HTTP status: {HttpStatusCode}. Response: {ResponseText}",
                (int)httpResponse.StatusCode,
                failureText);

            return null;
        }

        var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var cacheItem = JsonSerializer.Deserialize<TokenCacheItemJson>(json, SerializerOptions);

        var accessToken = cacheItem?.AccessToken.ToModel();
        if (accessToken is null)
        {
            return null;
        }

        if (accessToken.Value.ExpiresOn < GetMinExpirationTime())
        {
            return null;
        }

        return accessToken;
    }
}