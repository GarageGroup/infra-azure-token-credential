using System.Collections.Generic;
using System.Linq;
using Azure.Core;

namespace GarageGroup.Infra;

partial class AccessTokenInMemoryCache
{
    public IReadOnlyCollection<TokenRequestContext> GetContexts()
        =>
        inMemoryCache.Keys.ToArray();
}