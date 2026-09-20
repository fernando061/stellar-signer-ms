using StellarSigner.Application.Abstractions.Blockchain;
namespace StellarSigner.Application.Abstractions.Security;
public interface ISigningPolicy { void Validate(InspectedTransaction transaction, SigningContext context); }
public sealed record SigningContext(string WalletPublicKey, string Network, string AssetCode, string AssetIssuer,
    decimal Amount, string Destination, string AllowedContract, decimal MaxAmount, int MaxOperations, TimeSpan MaxRemaining);
