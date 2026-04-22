using System.Collections.Concurrent;
using Azure.Core;

namespace GarageGroup.Infra;

internal sealed partial class AccessTokenInMemoryCache
{
    internal static readonly AccessTokenInMemoryCache Instance
        =
        new();

    private AccessTokenInMemoryCache()
    {
    }

    private const int ExpirationPeriodInMinutes = 3;

    private readonly ConcurrentDictionary<TokenRequestContext, AccessToken> inMemoryCache
        =
        new(TokenRequestContextEqualityComparer.Instance);
}