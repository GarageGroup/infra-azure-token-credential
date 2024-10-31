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
            return TokenCredentialProvider.InternalResolveStandard(serviceProvider).GetTokenCredential();
        }
    }

    public static IServiceCollection AddRefreshableTokenCredentialStandardAsSingleton(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<TokenCredential>(ResolveRefreshableTokenCredentialStandard);

        static RefreshableTokenCredential ResolveRefreshableTokenCredentialStandard(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            return new(TokenCredentialProvider.InternalResolveStandard(serviceProvider));
        }
    }
}