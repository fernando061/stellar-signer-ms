using System.Security.Cryptography;
using StellarSigner.Application.Abstractions.Blockchain;
using StellarSigner.Application.Abstractions.Security;
using StellarSigner.Application.Common;
using StellarSigner.Infrastructure.DrivenAdapter.MasterKey;
using StellarSigner.Infrastructure.DrivenAdapter.SigningPolicies;
using StellarSigner.Infrastructure.DrivenAdapter.Stellar;
using StellarSigner.Domain.Enums;
using StellarDotnetSdk;
using StellarDotnetSdk.Accounts;
using StellarDotnetSdk.Operations;
using StellarDotnetSdk.Soroban;
using StellarDotnetSdk.Transactions;
using StellarSigner.Infrastructure.DrivenAdapter.Cryptography;
namespace StellarSigner.SecurityTests;
public sealed class SecurityBoundaryTests
{
    [Fact]
    public void AesGcmDetectsCiphertextAndVersionTampering()
    {
        using var protector = new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var plaintext = RandomNumberGenerator.GetBytes(65);
        var encrypted = protector.Protect(plaintext);
        Assert.NotEqual(Convert.ToHexString(plaintext), Convert.ToHexString(encrypted));
        Assert.Equal(plaintext, protector.Unprotect(encrypted));
        encrypted[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(encrypted));
        encrypted[0] = 2;
        Assert.Throws<CryptographicException>(() => protector.Unprotect(encrypted));
        CryptographicOperations.ZeroMemory(plaintext);
    }
    [Fact]
    public void MalformedXdrIsRejectedBeforeSigning()
    {
        var decoder = new StellarTransactionDecoder();
        var error = Assert.Throws<SignerException>(() => decoder.Decode("not-xdr", "Test SDF Network ; September 2015"));
        Assert.Equal("INVALID_TRANSACTION_XDR", error.Code);
    }
    [Fact]
    public void PoliciesRejectWrongIssuerContractSourceAndLimit()
    {
        var tx = new InspectedTransaction(new string('a', 64), "G-source", "C-contract", "lock_funds",
            SigningOperationType.LockFunds, "USDC", "G-issuer", 12m, "C-destination", DateTimeOffset.UtcNow.AddMinutes(2), 1);
        var context = new SigningContext("G-source", "Testnet", "USDC", "G-issuer", 12m, "C-destination",
            "C-contract", 20m, 1, TimeSpan.FromMinutes(5));
        Assert.Equal("ISSUER_NOT_ALLOWED", Assert.Throws<SignerException>(() => new AllowedAssetPolicy()
            .Validate(tx with { AssetIssuer = "G-other" }, context)).Code);
        Assert.Equal("CONTRACT_NOT_ALLOWED", Assert.Throws<SignerException>(() => new AllowedContractPolicy()
            .Validate(tx with { ContractId = "C-other" }, context)).Code);
        Assert.Equal("SOURCE_ACCOUNT_MISMATCH", Assert.Throws<SignerException>(() => new SourceAccountPolicy()
            .Validate(tx with { SourceAccount = "G-other" }, context)).Code);
        Assert.Equal("AMOUNT_LIMIT_EXCEEDED", Assert.Throws<SignerException>(() => new TransactionLimitPolicy()
            .Validate(tx with { Amount = 21m }, context)).Code);
        Assert.Equal("INVALID_TRANSACTION_XDR", Assert.Throws<SignerException>(() => new OperationCountPolicy()
            .Validate(tx with { OperationCount = 2 }, context)).Code);
    }
    [Fact]
    public void StellarAddressesRequireValidStrKeyChecksums()
    {
        var validator = new StellarAddressValidator();
        var account = "GDRXE2BQUC3AZNPVFSCEZ76NJ3WWL25FYFK6RGZGIEKWE4SOOHSUJUJ6";
        var contract = StrKey.EncodeContractId(new byte[32]);
        Assert.True(validator.IsValidAccount(account));
        Assert.True(validator.IsValidContract(contract));
        Assert.False(validator.IsValidAccount(account[..^1] + "A"));
        Assert.False(validator.IsValidContract(contract[..^1] + "A"));
    }
    [Fact]
    public void ValidSorobanEnvelopeIsInspectedAndSignedButExtraOperationIsRejected()
    {
        var seed = RandomNumberGenerator.GetBytes(32);
        try
        {
            var source = KeyPair.FromSecretSeed(seed).AccountId;
            var issuer = KeyPair.Random().AccountId;
            var destination = KeyPair.Random().AccountId;
            var contract = StrKey.EncodeContractId(RandomNumberGenerator.GetBytes(32));
            var args = new SCVal[] { new SCString("USDC"), new SCString(issuer), new SCInt128(0, 25000000), new SCString(destination) };
            var builder = new TransactionBuilder(new Account(source, 1))
                .AddOperation(new InvokeContractOperation(contract, "lock_funds", args, null))
                .AddTimeBounds(new TimeBounds(DateTimeOffset.UtcNow.AddSeconds(-5), TimeSpan.FromMinutes(3)))
                .SetSorobanTransactionData(new SorobanTransactionData(new SorobanResources(new LedgerFootprint(), 1000, 1000, 1000), 100, new SorobanResourceExtensionV0([])));
            var tx = builder.Build();
            var xdr = tx.ToUnsignedEnvelopeXdrBase64(TransactionBase.TransactionXdrVersion.V1);
            var decoder = new StellarTransactionDecoder();
            var inspected = decoder.Decode(xdr, "Test SDF Network ; September 2015");
            Assert.Equal(source, inspected.SourceAccount);
            Assert.Equal(contract, inspected.ContractId);
            Assert.Equal(2.5m, inspected.Amount);
            var signed = new Ed25519TransactionSigner().Sign(xdr, seed, "Test SDF Network ; September 2015");
            Assert.Single(Transaction.FromEnvelopeXdr(signed).Signatures);
            var withExtra = new TransactionBuilder(new Account(source, 1))
                .AddOperation(new InvokeContractOperation(contract, "lock_funds", args, null))
                .AddOperation(new InvokeContractOperation(contract, "lock_funds", args, null))
                .AddTimeBounds(new TimeBounds(DateTimeOffset.UtcNow.AddSeconds(-5), TimeSpan.FromMinutes(3)))
                .SetSorobanTransactionData(new SorobanTransactionData(new SorobanResources(new LedgerFootprint(), 1000, 1000, 1000), 100, new SorobanResourceExtensionV0([])))
                .Build().ToUnsignedEnvelopeXdrBase64(TransactionBase.TransactionXdrVersion.V1);
            Assert.Equal("INVALID_TRANSACTION_XDR", Assert.Throws<SignerException>(() => decoder.Decode(withExtra,
                "Test SDF Network ; September 2015")).Code);
        }
        finally { CryptographicOperations.ZeroMemory(seed); }
    }
}
