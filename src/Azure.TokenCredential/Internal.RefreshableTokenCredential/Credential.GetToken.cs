using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace GarageGroup.Infra;

partial class RefreshableTokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var accessToken = GetAccessTokenFromInMemoryCache(requestContext);
        if (accessToken is not null)
        {
            return accessToken.Value;
        }

        var newToken = innerCredentialProvider.GetTokenCredential().GetToken(requestContext, cancellationToken);
        return inMemoryCache.AddOrUpdate(requestContext, newToken, InnerGetNewToken);

        AccessToken InnerGetNewToken(TokenRequestContext _, AccessToken oldToken)
            =>
            newToken;
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var accessToken = GetAccessTokenFromInMemoryCache(requestContext);
        if (accessToken is not null)
        {
            return accessToken.Value;
        }

        var newToken = await innerCredentialProvider.GetTokenCredential().GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
        return inMemoryCache.AddOrUpdate(requestContext, newToken, InnerGetNewToken);

        AccessToken InnerGetNewToken(TokenRequestContext _, AccessToken oldToken)
            =>
            newToken;
    }

    private AccessToken? GetAccessTokenFromInMemoryCache(TokenRequestContext requestContext)
    {
        if (inMemoryCache.TryGetValue(requestContext, out var accessToken) is false)
        {
            return null;
        }

        var minExpirationTime = DateTimeOffset.Now.AddMinutes(ExpirationPeriodInMinutes);
        if (accessToken.ExpiresOn < minExpirationTime)
        {
            return null;
        }

        return accessToken;
    }
}