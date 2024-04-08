using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Azure.Core;

namespace GarageGroup.Infra;

partial class TokenCredentialHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Headers.Authorization is not null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var context = CreateRequestContext(request.RequestUri);
        if (context is null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var token = await tokenCredential.GetTokenAsync(context.Value, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = BuildAuthenticationHeaderValue(token);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private TokenRequestContext? CreateRequestContext(Uri? requestUri)
    {
        if (scopes.Length > 0)
        {
            return new(
                scopes: scopes);
        }

        if (requestUri is null)
        {
            return null;
        }

        return new(
            scopes:
            [
                new Uri(requestUri, ScopeRelativeUri).ToString()
            ]);
    }

    private AuthenticationHeaderValue BuildAuthenticationHeaderValue(AccessToken accessToken)
    {
        if (tokenType is TokenType.ResourceToken)
        {
            var token = HttpUtility.UrlEncode(string.Format(ResourceTokenTemplate, accessToken.Token));
            return new(token);
        }
        else
        {
            return new(AuthorizationScheme, accessToken.Token);
        }
    }
}