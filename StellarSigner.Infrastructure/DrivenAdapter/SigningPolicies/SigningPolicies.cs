using StellarSigner.Application.Abstractions.Blockchain;
using StellarSigner.Application.Abstractions.Security;
using StellarSigner.Application.Common;
using StellarDotnetSdk;
namespace StellarSigner.Infrastructure.DrivenAdapter.SigningPolicies;
public sealed class AllowedAssetPolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    {
        if (tx.AssetCode != "USDC") throw new SignerException("ASSET_NOT_ALLOWED", 422);
        if (tx.AssetIssuer != context.AssetIssuer) throw new SignerException("ISSUER_NOT_ALLOWED", 422);
    }
}
public sealed class AllowedDestinationPolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    {
        if (tx.Destination != context.Destination ||
            !(StrKey.IsValidEd25519PublicKey(tx.Destination) || StrKey.IsValidContractId(tx.Destination)))
            throw new SignerException("DESTINATION_NOT_ALLOWED", 422);
    }
}
public sealed class NetworkPassphrasePolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    { if (context.Network != "Testnet") throw new SignerException("NETWORK_MISMATCH", 422); }
}
public sealed class TransactionExpirationPolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    {
        var remaining = tx.Expiration - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero || remaining > context.MaxRemaining) throw new SignerException("TRANSACTION_EXPIRED", 422);
    }
}
public sealed class TransactionLimitPolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    { if (tx.Amount > context.MaxAmount) throw new SignerException("AMOUNT_LIMIT_EXCEEDED", 422); }
}
public sealed class AllowedContractPolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    { if (tx.ContractId != context.AllowedContract) throw new SignerException("CONTRACT_NOT_ALLOWED", 422); }
}
public sealed class AllowedFunctionPolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    { if (tx.FunctionName is not ("lock_funds" or "release_funds" or "refund_funds")) throw new SignerException("FUNCTION_NOT_ALLOWED", 422); }
}
public sealed class SourceAccountPolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    { if (tx.SourceAccount != context.WalletPublicKey) throw new SignerException("SOURCE_ACCOUNT_MISMATCH", 422); }
}
public sealed class OperationCountPolicy : ISigningPolicy
{
    public void Validate(InspectedTransaction tx, SigningContext context)
    { if (tx.OperationCount != 1 || tx.OperationCount > context.MaxOperations) throw new SignerException("INVALID_TRANSACTION_XDR", 400); }
}
