using StellarDotnetSdk;
using StellarDotnetSdk.Accounts;
using StellarDotnetSdk.Transactions;
using StellarSigner.Application.Abstractions.Cryptography;
using System.Security.Cryptography;
namespace StellarSigner.Infrastructure.DrivenAdapter.Cryptography;
public sealed class Ed25519TransactionSigner : ITransactionSigner
{
    public string Sign(string unsignedXdr, byte[] privateSeed, string networkPassphrase)
    {
        var transaction = Transaction.FromEnvelopeXdr(unsignedXdr);
        if (transaction.Signatures.Count != 0) throw new InvalidOperationException("Pre-signed transactions are not accepted");
        var pair = KeyPair.FromSecretSeed(privateSeed);
        try { transaction.Sign(pair, new Network(networkPassphrase)); }
        finally
        {
            if (pair.SeedBytes is { } pairSeed) CryptographicOperations.ZeroMemory(pairSeed);
            if (pair.PrivateKey is { } pairPrivate) CryptographicOperations.ZeroMemory(pairPrivate);
        }
        return transaction.ToEnvelopeXdrBase64(TransactionBase.TransactionXdrVersion.V1);
    }
}
