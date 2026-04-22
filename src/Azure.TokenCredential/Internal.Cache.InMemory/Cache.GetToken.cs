using System;
using Azure.Core;

namespace GarageGroup.Infra;

partial class AccessTokenInMemoryCache
{
    public AccessToken? GetToken(TokenRequestContext requestContext)
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