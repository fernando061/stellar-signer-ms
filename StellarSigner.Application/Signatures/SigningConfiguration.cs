namespace StellarSigner.Application.Signatures;
public sealed class SigningConfiguration
{
    public const string TestnetPassphrase = "Test SDF Network ; September 2015";
    public string NetworkPassphrase { get; set; } = TestnetPassphrase;
    public string Issuer { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public decimal MaxAmount { get; set; } = 10000m;
    public int MaxOperations { get; set; } = 1;
    public int MaxRemainingSeconds { get; set; } = 3600;
}
