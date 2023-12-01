using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class TokenCredentialServiceCollectionExtensions
{
    private const string StandardClientIdKey = "AZURE_CLIENT_ID";

    public static IServiceCollection AddTokenCredentialStandardAsSingleton(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton(ResolveTokenCredential);
    }

    private static TokenCredential ResolveTokenCredential(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var clientId = serviceProvider.GetRequiredService<IConfiguration>()[StandardClientIdKey];

        if (string.IsNullOrEmpty(clientId))
        {
            return new AzureCliCredential();
        }

        return new ManagedIdentityCredential(clientId: clientId);
    }
}