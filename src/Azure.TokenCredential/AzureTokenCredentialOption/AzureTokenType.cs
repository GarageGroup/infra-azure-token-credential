namespace GarageGroup.Infra;

public enum AzureTokenType
{
    AzureCli,

    SystemAssignedManagedIdentity,

    UserAssignedManagedIdentity,

    ClientSecret
}