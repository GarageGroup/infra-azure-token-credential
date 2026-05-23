using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GarageGroup.Infra;

internal static class AzureTokenCredentialOptionResolver
{
    private const string TokenCredentialSectionName = "AzureTokenCredential";

    private const string TokenCredentialTokenTypeKey = "TokenType";

    private const string TokenCredentialTenantIdKey = "TenantId";

    private const string TokenCredentialClientIdKey = "ClientId";

    private const string TokenCredentialClientSecretKey = "ClientSecret";

    private const string StandardTokenTypeKey = "AZURE_TOKEN_TYPE";

    private const string StandardTenantIdKey = "AZURE_TENANT_ID";

    private const string StandardClientIdKey = "AZURE_CLIENT_ID";

    private const string StandardClientSecretKey = "AZURE_CLIENT_SECRET";

    internal static AzureTokenCredentialOption ResolveStandard(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var section = configuration.GetSection(TokenCredentialSectionName);
        if (section.Exists())
        {
            return section.ReadFromSection();
        }

        return new()
        {
            TokenType = configuration.GetAzureTokenType(StandardTokenTypeKey),
            TenantId = configuration[StandardTenantIdKey],
            ClientId = configuration[StandardClientIdKey],
            ClientSecret = configuration[StandardClientSecretKey],
        };
    }

    private static AzureTokenCredentialOption ReadFromSection(this IConfigurationSection section)
        =>
        new()
        {
            TokenType = section.GetAzureTokenType(TokenCredentialTokenTypeKey),
            TenantId = section[TokenCredentialTenantIdKey],
            ClientId = section[TokenCredentialClientIdKey],
            ClientSecret = section[TokenCredentialClientSecretKey],
        };

    private static AzureTokenType? GetAzureTokenType(this IConfiguration configuration, string tokenTypeKey)
    {
        var tokenTypeText = configuration[tokenTypeKey];
        if (string.IsNullOrWhiteSpace(tokenTypeText))
        {
            return null;
        }

        if (Enum.TryParse<AzureTokenType>(tokenTypeText, true, out var tokenType) is false)
        {
            throw new InvalidOperationException(
                $"Unexpected {tokenTypeKey} value: '{tokenTypeText}'. Available values: {string.Join(", ", Enum.GetNames<AzureTokenType>())}.");
        }

        return tokenType;
    }
}