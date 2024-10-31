using Azure.Core;
using Azure.Identity;

namespace GarageGroup.Infra;

partial class TokenCredentialProvider
{
    public TokenCredential GetTokenCredential()
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new AzureCliCredential();
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            return new ManagedIdentityCredential(clientId: clientId);
        }

        return new ClientSecretCredential(
            tenantId: tenantId,
            clientId: clientId,
            clientSecret: clientSecret);
    }
}