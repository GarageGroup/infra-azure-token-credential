using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GarageGroup.Infra;

internal static class AzureTokenCredentialOptionResolver
{
    private const string StandardTokenTypeKey = "AZURE_TOKEN_TYPE";

    private const string StandardTenantIdKey = "AZURE_TENANT_ID";

    private const string StandardClientIdKey = "AZURE_CLIENT_ID";

    private const string StandardClientSecretKey = "AZURE_CLIENT_SECRET";

    internal static AzureTokenCredentialOption ResolveStandard(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var tokenTypeText = configuration[StandardTokenTypeKey];
        if (Enum.TryParse<AzureTokenType>(tokenTypeText, true, out var tokenType) is false)
        {
            throw new InvalidOperationException(
                $"Unexpected {StandardTokenTypeKey} value: '{tokenTypeText}'. " +
                $"Available values: {string.Join(", ", Enum.GetNames<AzureTokenType>())}.");
        }

        return new()
        {
            TokenType = tokenType,
            TenantId = configuration[StandardTenantIdKey],
            ClientId = configuration[StandardClientIdKey],
            ClientSecret = configuration[StandardClientSecretKey],
        };
    }

    private static AzureTokenType? GetAzureTokenType(this IConfiguration configuration)
    {
        var tokenTypeText = configuration[StandardTokenTypeKey];
        if (string.IsNullOrWhiteSpace(tokenTypeText))
        {
            return null;
        }

        if (Enum.TryParse<AzureTokenType>(tokenTypeText, true, out var tokenType) is false)
        {
            throw new InvalidOperationException(
                $"Unexpected {StandardTokenTypeKey} value: '{tokenTypeText}'. " +
                $"Available values: {string.Join(", ", Enum.GetNames<AzureTokenType>())}.");
        }

        return tokenType;
    }
}