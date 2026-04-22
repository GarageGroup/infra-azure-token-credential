using System.Collections.Generic;
using System.Collections.Concurrent;
using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace GarageGroup.Infra;

partial class RefreshableTokenCredential
{
    public async Task RefreshTokensAsync(CancellationToken cancellationToken)
    {
        var contexts = await GetContextsAsync(cancellationToken).ConfigureAwait(false);
        if (contexts.Count is 0)
        {
            return;
        }

        ConcurrentQueue<Exception> failures = [];

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(contexts, parallelOptions, InnerRefreshAsync).ConfigureAwait(false);

        if (failures.TryDequeue(out var firstFailure) is false)
        {
            return;
        }

        if (failures.IsEmpty)
        {
            throw firstFailure;
        }

        List<Exception> allFailures = [firstFailure];
        while (failures.TryDequeue(out var failure))
        {
            allFailures.Add(failure);
        }

        throw new AggregateException("Failed to refresh one or more access tokens.", allFailures);

        async ValueTask InnerRefreshAsync(TokenRequestContext context, CancellationToken cancellationToken)
        {
            try
            {
                var newToken = await credentialProvider.GetTokenCredential().GetTokenAsync(context, cancellationToken).ConfigureAwait(false);

                inMemoryCache.SaveToken(context, newToken);
                if (remoteCache is not null)
                {
                    await remoteCache.SaveTokenAsync(context, newToken, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var scopes = context.Scopes is null ? string.Empty : string.Join(", ", context.Scopes);
                failures.Enqueue(new InvalidOperationException(
                    $"Unable to refresh access token for scopes: '{scopes}'.",
                    exception));
            }
        }
    }

    private async Task<IReadOnlyCollection<TokenRequestContext>> GetContextsAsync(CancellationToken cancellationToken)
    {
        var inMemoryContexts = inMemoryCache.GetContexts();
        if (remoteCache is null)
        {
            return inMemoryContexts;
        }

        var remoteContexts = await remoteCache.GetContextsAsync(cancellationToken).ConfigureAwait(false);
        if (remoteContexts.Count is 0)
        {
            return inMemoryContexts;
        }

        if (inMemoryContexts.Count is 0)
        {
            return remoteContexts;
        }

        var contexts = new HashSet<TokenRequestContext>(
            collection: inMemoryContexts,
            comparer: TokenRequestContextEqualityComparer.Instance);

        foreach (var remoteContext in remoteContexts)
        {
            _ = contexts.Add(remoteContext);
        }

        return contexts;
    }
}
