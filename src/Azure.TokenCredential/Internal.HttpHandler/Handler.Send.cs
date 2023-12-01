using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
        request.Headers.Authorization = new(AuthorizationScheme, token.Token);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}