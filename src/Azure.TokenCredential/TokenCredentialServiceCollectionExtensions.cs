using System;
using Azure.Core;
using GarageGroup.Infra;

namespace Microsoft.Extensions.DependencyInjection;

public static class TokenCredentialServiceCollectionExtensions
{
    public static IServiceCollection AddTokenCredentialStandardAsSingleton(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton(ResolveTokenCredentialStandard);

        static TokenCredential ResolveTokenCredentialStandard(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var provider = new TokenCredentialProvider(
                option: AzureTokenCredentialOptionResolver.ResolveStandard(serviceProvider));

            return provider.GetTokenCredential();
        }
    }

    public static IServiceCollection AddKeyedTokenCredentialSingleton(
        this IServiceCollection services,
        Func<IServiceProvider, AzureTokenCredentialOption> optionResolver,
        object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionResolver);

        return services.AddKeyedSingleton(serviceKey, ResolveTokenCredentialStandard);

        TokenCredential ResolveTokenCredentialStandard(IServiceProvider serviceProvider, object? _)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var provider = new TokenCredentialProvider(
                option: optionResolver.Invoke(serviceProvider));

            return provider.GetTokenCredential();
        }
    }

    public static IServiceCollection AddRefreshableTokenCredentialStandardAsSingleton(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<TokenCredential>(ResolveRefreshableTokenCredentialStandard);

        static RefreshableTokenCredential ResolveRefreshableTokenCredentialStandard(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            return new(
                credentialProvider: new(
                    option: AzureTokenCredentialOptionResolver.ResolveStandard(serviceProvider)),
                inMemoryCache: AccessTokenInMemoryCache.Default,
                remoteCache: AccessTokenRemoteCache.InternalResolveStandard(serviceProvider));
        }
    }

    public static IServiceCollection AddKeyedRefreshableTokenCredentialAsSingleton(
        this IServiceCollection services,
        Func<IServiceProvider, AzureTokenCredentialOption> optionResolver,
        string tagName,
        object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionResolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        return services.AddKeyedSingleton(serviceKey, ResolveRefreshableTokenCredentialStandard);

        TokenCredential ResolveRefreshableTokenCredentialStandard(IServiceProvider serviceProvider, object? _)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            return new RefreshableTokenCredential(
                credentialProvider: new(
                    option: optionResolver.Invoke(serviceProvider)),
                inMemoryCache: new(),
                remoteCache: AccessTokenRemoteCache.InternalResolveStandard(serviceProvider, tagName));
        }
    }
}