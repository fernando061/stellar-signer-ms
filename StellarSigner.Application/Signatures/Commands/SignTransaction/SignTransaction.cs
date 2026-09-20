using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using StellarSigner.Application.Abstractions.Blockchain;
using StellarSigner.Application.Abstractions.Cryptography;
using StellarSigner.Application.Abstractions.Persistence;
using StellarSigner.Application.Abstractions.Security;
using StellarSigner.Application.Common;
using StellarSigner.Domain.Entities;
using StellarSigner.Domain.Enums;
namespace StellarSigner.Application.Signatures.Commands.SignTransaction;
public sealed record ExpectedTransaction(string Network, string AssetCode, string AssetIssuer, decimal Amount, string Destination);
public sealed record SignTransactionCommand(Guid RequestId, Guid RemittanceId, Guid PartnerId, SigningOperationType OperationType,
    string UnsignedTransactionXdr, ExpectedTransaction ExpectedTransaction, string IdempotencyKey, string CallerId) : IRequest<SignTransactionResult>;
public sealed record SignTransactionResult(Guid RequestId, string TransactionHash, string SignedTransactionXdr, string Status, DateTimeOffset SignedAt);
public sealed class SignTransactionValidator : AbstractValidator<SignTransactionCommand>
{
    public SignTransactionValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty(); RuleFor(x => x.RemittanceId).NotEmpty(); RuleFor(x => x.PartnerId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(160);
        RuleFor(x => x.UnsignedTransactionXdr).NotEmpty().MaximumLength(131072);
        RuleFor(x => x.CallerId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExpectedTransaction).NotNull();
        When(x => x.ExpectedTransaction is not null, () => {
            RuleFor(x => x.ExpectedTransaction.Amount).GreaterThan(0).PrecisionScale(20, 7, true);
            RuleFor(x => x.ExpectedTransaction.AssetCode).NotEmpty().MaximumLength(12);
            RuleFor(x => x.ExpectedTransaction.AssetIssuer).NotEmpty().MaximumLength(56);
            RuleFor(x => x.ExpectedTransaction.Destination).NotEmpty().MaximumLength(56);
        });
    }
}
public sealed class SignTransactionHandler(IPartnerWalletRepository wallets, ISigningRequestRepository requests, IAuditRepository audits,
    IUnitOfWork unitOfWork, ITransactionDecoder decoder, IEnumerable<ISigningPolicy> policies,
    IKeyDerivationService derivation, ITransactionSigner signer, SigningConfiguration configuration)
    : IRequestHandler<SignTransactionCommand, SignTransactionResult>
{
    public async Task<SignTransactionResult> Handle(SignTransactionCommand command, CancellationToken ct)
    {
        var payloadHash = Hash(JsonSerializer.Serialize(new
        {
            command.RequestId, command.RemittanceId, command.PartnerId, command.CallerId, command.OperationType,
            command.UnsignedTransactionXdr, command.ExpectedTransaction.Network, command.ExpectedTransaction.AssetCode,
            command.ExpectedTransaction.AssetIssuer,
            Amount = command.ExpectedTransaction.Amount.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
            command.ExpectedTransaction.Destination
        }));
        await using var transaction = await unitOfWork.BeginSigningTransactionAsync(command.IdempotencyKey, command.RequestId, ct);
        var old = await requests.ByIdempotencyKeyAsync(command.IdempotencyKey, ct);
        if (old is not null)
        {
            if (old.PayloadHash == payloadHash && old.Status == SigningStatus.Signed &&
                old.TransactionHash is not null && old.SignedXdr is not null && old.SignedAt is not null)
                return new SignTransactionResult(old.RequestId, old.TransactionHash, old.SignedXdr, old.Status.ToString(), old.SignedAt.Value);
            if (old.PayloadHash == payloadHash && old.Status == SigningStatus.Rejected && old.FailureCode is not null)
                throw new SignerException(old.FailureCode, old.FailureStatusCode ?? 422);
            await RecordRejectionAsync(command, payloadHash, null, null, "SIGNING_REQUEST_CONFLICT", 409, false, ct);
            await transaction.CommitAsync(ct);
            throw new SignerException("SIGNING_REQUEST_CONFLICT", 409);
        }
        if (await requests.ByRequestIdAsync(command.RequestId, ct) is not null)
        {
            await RecordRejectionAsync(command, payloadHash, null, null, "SIGNING_REQUEST_CONFLICT", 409, false, ct);
            await transaction.CommitAsync(ct);
            throw new SignerException("SIGNING_REQUEST_CONFLICT", 409);
        }
        PartnerWallet? wallet = null;
        InspectedTransaction? inspected = null;
        try
        {
            wallet = await wallets.GetAsync(command.PartnerId, ct) ?? throw new SignerException("WALLET_NOT_FOUND", 404);
            if (wallet.Status != WalletStatus.Active) throw new SignerException("WALLET_NOT_ACTIVE", 403);
            if (command.ExpectedTransaction.Network != "Testnet") throw new SignerException("NETWORK_MISMATCH", 422);
            inspected = decoder.Decode(command.UnsignedTransactionXdr, configuration.NetworkPassphrase);
            if (inspected.OperationType != command.OperationType || inspected.AssetCode != command.ExpectedTransaction.AssetCode ||
                inspected.AssetIssuer != command.ExpectedTransaction.AssetIssuer || inspected.Amount != command.ExpectedTransaction.Amount ||
                inspected.Destination != command.ExpectedTransaction.Destination)
                throw new SignerException("TRANSACTION_CONTEXT_MISMATCH", 422);
            var context = new SigningContext(wallet.PublicKey, command.ExpectedTransaction.Network,
                command.ExpectedTransaction.AssetCode, configuration.Issuer, command.ExpectedTransaction.Amount,
                command.ExpectedTransaction.Destination, configuration.ContractId, configuration.MaxAmount,
                configuration.MaxOperations, TimeSpan.FromSeconds(configuration.MaxRemainingSeconds));
            foreach (var policy in policies) policy.Validate(inspected, context);
            using var derived = derivation.Derive(checked((uint)wallet.DerivationIndex));
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(derived.PublicKey), Encoding.ASCII.GetBytes(wallet.PublicKey)))
                throw new SignerException("MASTER_KEY_UNAVAILABLE", 503);
            string signedXdr;
            try { signedXdr = signer.Sign(command.UnsignedTransactionXdr, derived.Seed, configuration.NetworkPassphrase); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or CryptographicException)
            { throw new SignerException("SIGNATURE_FAILED", 500); }
            var request = SigningRequest.Create(command.RequestId, command.IdempotencyKey, payloadHash, command.RemittanceId, wallet,
                inspected.TransactionHash, Hash(command.UnsignedTransactionXdr), command.OperationType, inspected.AssetCode,
                inspected.AssetIssuer, inspected.Amount, inspected.Destination);
            request.MarkSigned(signedXdr, Hash(signedXdr));
            await requests.AddAsync(request, ct);
            await audits.AddAsync(AuditRecord.ForSigning(command.RequestId, command.RemittanceId, command.PartnerId, wallet.PublicKey,
                inspected.TransactionHash, command.OperationType, inspected.Amount, inspected.AssetCode, inspected.Destination,
                "Signed", null, command.CallerId), ct);
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new SignTransactionResult(command.RequestId, inspected.TransactionHash, signedXdr, "Signed", request.SignedAt!.Value);
        }
        catch (SignerException ex)
        {
            await RecordRejectionAsync(command, payloadHash, wallet, inspected, ex.Code, ex.Status, true, ct);
            await transaction.CommitAsync(ct);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            await RecordRejectionAsync(command, payloadHash, wallet, inspected, "MASTER_KEY_UNAVAILABLE", 503, true, ct);
            await transaction.CommitAsync(ct);
            throw new SignerException("MASTER_KEY_UNAVAILABLE", 503);
        }
    }
    private async Task RecordRejectionAsync(SignTransactionCommand command, string payloadHash, PartnerWallet? wallet,
        InspectedTransaction? inspected, string code, int status, bool persistRequest, CancellationToken ct)
    {
        if (persistRequest)
            await requests.AddAsync(SigningRequest.CreateRejected(command.RequestId, command.IdempotencyKey, payloadHash,
                command.RemittanceId, command.PartnerId, wallet?.Id, wallet?.PublicKey, inspected?.TransactionHash,
                Hash(command.UnsignedTransactionXdr), command.OperationType, command.ExpectedTransaction.AssetCode,
                command.ExpectedTransaction.AssetIssuer, command.ExpectedTransaction.Amount,
                command.ExpectedTransaction.Destination, code, status), ct);
        await audits.AddAsync(AuditRecord.ForSigning(command.RequestId, command.RemittanceId, command.PartnerId,
            wallet?.PublicKey, inspected?.TransactionHash, command.OperationType, command.ExpectedTransaction.Amount,
            command.ExpectedTransaction.AssetCode, command.ExpectedTransaction.Destination, "Rejected", code, command.CallerId), ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
