using System;
using Azure.Core;
using Azure.Identity;

namespace GarageGroup.Infra;

partial class TokenCredentialProvider
{
    public TokenCredential GetTokenCredential()
        =>
        option.TokenType switch
        {
            AzureTokenType.AzureCli => new AzureCliCredential(),
            AzureTokenType.SystemAssignedManagedIdentity => BuildSystemAssignedManagedIdentity(),
            AzureTokenType.UserAssignedManagedIdentity => BuildUserAssignedManagedIdentity(),
            AzureTokenType.ClientSecret => BuildClientSecretCredential(),
            _ => ResolveDefaultTokenCredential()
        };

    private TokenCredential ResolveDefaultTokenCredential()
    {
        if (string.IsNullOrWhiteSpace(option.ClientId))
        {
            return BuildSystemAssignedManagedIdentity();
        }

        if (string.IsNullOrWhiteSpace(option.ClientSecret))
        {
            return BuildUserAssignedManagedIdentity();
        }

        return BuildClientSecretCredential();
    }

    private static ManagedIdentityCredential BuildSystemAssignedManagedIdentity()
        =>
        new(
            id: ManagedIdentityId.SystemAssigned);

    private ManagedIdentityCredential BuildUserAssignedManagedIdentity()
    {
        if (string.IsNullOrWhiteSpace(option.ClientId))
        {
            throw new InvalidOperationException("Azure Client ID must be specified for user assigned managed identity credential.");
        }

        return new(
            id: ManagedIdentityId.FromUserAssignedClientId(option.ClientId));
    }

    private ClientSecretCredential BuildClientSecretCredential()
    {
        if (string.IsNullOrWhiteSpace(option.TenantId))
        {
            throw new InvalidOperationException("Azure Tenant ID must be specified for client secret credential.");
        }

        if (string.IsNullOrWhiteSpace(option.ClientId))
        {
            throw new InvalidOperationException("Azure Client ID must be specified for client secret credential.");
        }

        if (string.IsNullOrWhiteSpace(option.ClientSecret))
        {
            throw new InvalidOperationException("Azure Client Secret must be specified for client secret credential.");
        }

        return new(
            tenantId: option.TenantId,
            clientId: option.ClientId,
            clientSecret: option.ClientSecret);
    }
}
