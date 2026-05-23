using System.Collections.Concurrent;
using Azure.Core;

namespace GarageGroup.Infra;

internal sealed partial class AccessTokenInMemoryCache
{
    internal static readonly AccessTokenInMemoryCache Default
        =
        new();

    private const int ExpirationPeriodInMinutes = 3;

    private readonly ConcurrentDictionary<TokenRequestContext, AccessToken> inMemoryCache
        =
        new(TokenRequestContextEqualityComparer.Instance);
}