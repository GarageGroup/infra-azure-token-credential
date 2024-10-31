using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GarageGroup.Infra;

internal sealed partial class TokenCredentialProvider
{
    internal static TokenCredentialProvider InternalResolveStandard(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        return new(
            tenantId: configuration[StandardTenantIdKey],
            clientId: configuration[StandardClientIdKey],
            clientSecret: configuration[StandardClientSecretKey]);
    }

    private const string StandardTenantIdKey = "AZURE_TENANT_ID";

    private const string StandardClientIdKey = "AZURE_CLIENT_ID";

    private const string StandardClientSecretKey = "AZURE_CLIENT_SECRET";

    private readonly string? tenantId;

    private readonly string? clientId;

    private readonly string? clientSecret;

    private TokenCredentialProvider(string? tenantId, string? clientId, string? clientSecret)
    {
        this.tenantId = tenantId;
        this.clientId = clientId;
        this.clientSecret = clientSecret;
    }
}