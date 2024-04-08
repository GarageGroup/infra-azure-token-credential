using System;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class TokenCredentialServiceCollectionExtensions
{
    private const string StandardTenantIdKey = "AZURE_TENANT_ID";

    private const string StandardClientIdKey = "AZURE_CLIENT_ID";

    private const string StandardClientSecretKey = "AZURE_CLIENT_SECRET";

    public static IServiceCollection AddTokenCredentialStandardAsSingleton(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton(ResolveTokenCredential);
    }

    private static TokenCredential ResolveTokenCredential(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var clientId = configuration[StandardClientIdKey];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new AzureCliCredential();
        }

        var clientSecret = configuration[StandardClientSecretKey];
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            return new ManagedIdentityCredential(clientId: clientId);
        }

        var tenantId = configuration[StandardTenantIdKey];
        return new ClientSecretCredential(tenantId: tenantId, clientId: clientId, clientSecret: clientSecret);
    }
}