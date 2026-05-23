namespace GarageGroup.Infra;

public sealed record class AzureTokenCredentialOption
{
    public AzureTokenType? TokenType { get; init; }

    public string? TenantId { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret  { get; init; }
}