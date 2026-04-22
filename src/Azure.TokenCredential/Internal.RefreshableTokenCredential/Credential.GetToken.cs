using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace GarageGroup.Infra;

partial class RefreshableTokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var accessToken = inMemoryCache.GetToken(requestContext);
        if (accessToken is not null)
        {
            return accessToken.Value;
        }

        if (remoteCache is not null)
        {
            accessToken = remoteCache.GetTokenAsync(requestContext, cancellationToken).AsTask().Result;
            if (accessToken is not null)
            {
                inMemoryCache.SaveToken(requestContext, accessToken.Value);
                return accessToken.Value;
            }
        }

        var newToken = credentialProvider.GetTokenCredential().GetToken(requestContext, cancellationToken);

        inMemoryCache.SaveToken(requestContext, newToken);
        remoteCache?.SaveTokenAsync(requestContext, newToken, cancellationToken).Wait(cancellationToken);

        return newToken;
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var accessToken = inMemoryCache.GetToken(requestContext);
        if (accessToken is not null)
        {
            return accessToken.Value;
        }

        if (remoteCache is not null)
        {
            accessToken = await remoteCache.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
            if (accessToken is not null)
            {
                inMemoryCache.SaveToken(requestContext, accessToken.Value);
                return accessToken.Value;
            }
        }

        var newToken = await credentialProvider.GetTokenCredential().GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);

        inMemoryCache.SaveToken(requestContext, newToken);
        if (remoteCache is not null)
        {
            await remoteCache.SaveTokenAsync(requestContext, newToken, cancellationToken).ConfigureAwait(false);
        }

        return newToken;
    }
}
