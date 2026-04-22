using System;
using Azure.Core;
using Azure.Identity;

namespace GarageGroup.Infra;

partial class TokenCredentialProvider
{
    public TokenCredential GetTokenCredential()
    {
        if (string.IsNullOrWhiteSpace(tokenType))
        {
            return ResolveDefaultTokenCredential();
        }

        if (IsTokenType("AzureCli"))
        {
            return new AzureCliCredential();
        }

        if (IsTokenType("SystemAssignedManagedIdentity"))
        {
            return new ManagedIdentityCredential(
                id: ManagedIdentityId.SystemAssigned);
        }

        if (IsTokenType("UserAssignedManagedIdentity"))
        {
            return new ManagedIdentityCredential(
                id: ManagedIdentityId.FromUserAssignedClientId(clientId));
        }

        if (IsTokenType("ClientSecret"))
        {
            return new ClientSecretCredential(
                tenantId: tenantId,
                clientId: clientId,
                clientSecret: clientSecret);
        }

        throw new InvalidOperationException(
            $"Unsupported AZURE_TOKEN_TYPE '{tokenType}'. Supported values: AzureCli, " +
            "SystemAssignedManagedIdentity, UserAssignedManagedIdentity, ClientSecret.");

        bool IsTokenType(string value)
            =>
            string.Equals(tokenType, value, StringComparison.OrdinalIgnoreCase);
    }

    private TokenCredential ResolveDefaultTokenCredential()
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new ManagedIdentityCredential(
                id: ManagedIdentityId.SystemAssigned);
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            return new ManagedIdentityCredential(
                id: ManagedIdentityId.FromUserAssignedClientId(clientId));
        }

        return new ClientSecretCredential(
            tenantId: tenantId,
            clientId: clientId,
            clientSecret: clientSecret);
    }
}
