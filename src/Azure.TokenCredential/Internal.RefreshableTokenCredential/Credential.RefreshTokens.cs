using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace GarageGroup.Infra;

partial class RefreshableTokenCredential
{
    public Task RefreshTokensAsync(CancellationToken cancellationToken)
    {
        if (inMemoryCache.IsEmpty)
        {
            return Task.CompletedTask;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken
        };

        return Parallel.ForEachAsync(inMemoryCache.Keys, parallelOptions, InnerRefreshAsync);

        async ValueTask InnerRefreshAsync(TokenRequestContext context, CancellationToken cancellationToken)
        {
            var newToken = await innerCredentialProvider.GetTokenCredential().GetTokenAsync(context, cancellationToken).ConfigureAwait(false);
            _ = inMemoryCache.AddOrUpdate(context, newToken, InnerGetNewToken);

            AccessToken InnerGetNewToken(TokenRequestContext _, AccessToken oldToken)
            {
                return newToken;
            }
        }
    }
}