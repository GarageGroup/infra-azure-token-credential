using Azure.Core;

namespace GarageGroup.Infra;

partial class AccessTokenInMemoryCache
{
    public void SaveToken(TokenRequestContext requestContext, AccessToken accessToken)
    {
        _ = inMemoryCache.AddOrUpdate(requestContext, accessToken, InnerGetNewToken);

        AccessToken InnerGetNewToken(TokenRequestContext _, AccessToken oldToken)
            =>
            accessToken;
    }
}