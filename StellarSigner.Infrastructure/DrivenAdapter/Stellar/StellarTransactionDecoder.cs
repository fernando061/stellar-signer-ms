using StellarDotnetSdk;
using StellarDotnetSdk.Memos;
using StellarDotnetSdk.Operations;
using StellarDotnetSdk.Soroban;
using StellarDotnetSdk.Transactions;
using StellarSigner.Application.Abstractions.Blockchain;
using StellarSigner.Application.Common;
using StellarSigner.Domain.Enums;
namespace StellarSigner.Infrastructure.DrivenAdapter.Stellar;
public sealed class StellarTransactionDecoder : ITransactionDecoder
{
    public InspectedTransaction Decode(string xdr, string networkPassphrase)
    {
        try { return Inspect(xdr, networkPassphrase); }
        catch (SignerException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or IndexOutOfRangeException or OverflowException)
        { throw new SignerException("INVALID_TRANSACTION_XDR", 400); }
    }
    private static InspectedTransaction Inspect(string xdr, string networkPassphrase)
    {
        if (xdr.Length > 131072) throw new SignerException("INVALID_TRANSACTION_XDR", 400);
        var tx = Transaction.FromEnvelopeXdr(xdr);
        if (tx.Signatures.Count != 0 || tx.Operations.Length != 1 || tx.Operations[0] is not InvokeContractOperation op ||
            tx.SorobanTransactionData is null || tx.Memo is not MemoNone)
            throw new SignerException("INVALID_TRANSACTION_XDR", 400);
        var pre = tx.Preconditions;
        if (pre is null || pre.ExtraSigners?.Count > 0 || pre.LedgerBounds is not null ||
            pre.MinSequenceNumber is not null || pre.MinSequenceAge > 0 || pre.MinSequenceLedgerGap > 0)
            throw new SignerException("INVALID_TRANSACTION_XDR", 400);
        if (op.SourceAccount is not null && op.SourceAccount.AccountId != tx.SourceAccount.AccountId)
            throw new SignerException("SOURCE_ACCOUNT_MISMATCH", 422);
        if (op.Auth.Length != 0) throw new SignerException("INVALID_TRANSACTION_XDR", 400);
        var bounds = tx.TimeBounds ?? throw new SignerException("TRANSACTION_EXPIRED", 422);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (bounds.MaxTime <= now || bounds.MinTime > now || bounds.MaxTime == 0)
            throw new SignerException("TRANSACTION_EXPIRED", 422);
        var host = op.HostFunction;
        if (host.ContractAddress is not ScContractId contract || host.Args.Length != 4 ||
            host.Args[0] is not SCString asset || host.Args[1] is not SCString issuer ||
            host.Args[2] is not SCInt128 amount || host.Args[3] is not SCString destination || amount.Hi != 0)
            throw new SignerException("TRANSACTION_CONTEXT_MISMATCH", 422);
        var operation = host.FunctionName.InnerValue switch
        {
            "lock_funds" => SigningOperationType.LockFunds,
            "release_funds" => SigningOperationType.ReleaseFunds,
            "refund_funds" => SigningOperationType.RefundFunds,
            _ => throw new SignerException("FUNCTION_NOT_ALLOWED", 422)
        };
        var value = amount.Lo / 10000000m;
        if (value <= 0 || decimal.Round(value, 7) != value) throw new SignerException("TRANSACTION_CONTEXT_MISMATCH", 422);
        var hash = Convert.ToHexString(tx.Hash(new Network(networkPassphrase))).ToLowerInvariant();
        return new InspectedTransaction(hash, tx.SourceAccount.AccountId, contract.InnerValue,
            host.FunctionName.InnerValue, operation, asset.InnerValue, issuer.InnerValue, value,
            destination.InnerValue, DateTimeOffset.FromUnixTimeSeconds(bounds.MaxTime), tx.Operations.Length);
    }
}
