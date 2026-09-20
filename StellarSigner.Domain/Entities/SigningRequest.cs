using StellarSigner.Domain.Enums;
namespace StellarSigner.Domain.Entities;
public sealed class SigningRequest
{
    private SigningRequest() { }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string PayloadHash { get; private set; } = null!;
    public Guid RemittanceId { get; private set; }
    public Guid PartnerId { get; private set; }
    public Guid? WalletId { get; private set; }
    public PartnerWallet? Wallet { get; private set; }
    public string? PublicKey { get; private set; }
    public string? TransactionHash { get; private set; }
    public string UnsignedXdrHash { get; private set; } = null!;
    public string? SignedXdrHash { get; private set; }
    public string? SignedXdr { get; private set; }
    public SigningOperationType OperationType { get; private set; }
    public string AssetCode { get; private set; } = null!;
    public string AssetIssuer { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Destination { get; private set; } = null!;
    public SigningStatus Status { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureReason { get; private set; }
    public int? FailureStatusCode { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? SignedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public static SigningRequest Create(Guid requestId, string key, string payloadHash, Guid remittanceId, PartnerWallet wallet,
        string txHash, string xdrHash, SigningOperationType operation, string assetCode, string assetIssuer, decimal amount, string destination)
    {
        if (requestId == Guid.Empty || remittanceId == Guid.Empty || string.IsNullOrWhiteSpace(key) || amount <= 0 || wallet.Status != WalletStatus.Active)
            throw new ArgumentException("Invalid signing request");
        var now = DateTimeOffset.UtcNow;
        return new SigningRequest { RequestId = requestId, IdempotencyKey = key, PayloadHash = payloadHash,
            RemittanceId = remittanceId, PartnerId = wallet.PartnerId, WalletId = wallet.Id,
            PublicKey = wallet.PublicKey, TransactionHash = txHash, UnsignedXdrHash = xdrHash,
            OperationType = operation, AssetCode = assetCode, AssetIssuer = assetIssuer, Amount = amount, Destination = destination,
            Status = SigningStatus.Pending, RequestedAt = now, CreatedAt = now, UpdatedAt = now };
    }
    public static SigningRequest CreateRejected(Guid requestId, string key, string payloadHash, Guid remittanceId, Guid partnerId,
        Guid? walletId, string? publicKey, string? transactionHash, string unsignedXdrHash,
        SigningOperationType operation, string assetCode, string assetIssuer, decimal amount, string destination,
        string failureCode, int failureStatusCode)
    {
        if (requestId == Guid.Empty || remittanceId == Guid.Empty || partnerId == Guid.Empty || amount <= 0 ||
            string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(failureCode))
            throw new ArgumentException("Invalid rejected signing request");
        var now = DateTimeOffset.UtcNow;
        return new SigningRequest { RequestId = requestId, IdempotencyKey = key, PayloadHash = payloadHash,
            RemittanceId = remittanceId, PartnerId = partnerId, WalletId = walletId, PublicKey = publicKey,
            TransactionHash = transactionHash, UnsignedXdrHash = unsignedXdrHash, OperationType = operation,
            AssetCode = assetCode, AssetIssuer = assetIssuer, Amount = amount, Destination = destination,
            Status = SigningStatus.Rejected, FailureCode = failureCode, FailureStatusCode = failureStatusCode,
            RequestedAt = now, CreatedAt = now, UpdatedAt = now };
    }
    public void MarkSigned(string signedXdr, string signedXdrHash)
    {
        if (Status != SigningStatus.Pending) throw new InvalidOperationException("Request already completed");
        SignedXdr = signedXdr; SignedXdrHash = signedXdrHash; Status = SigningStatus.Signed;
        SignedAt = DateTimeOffset.UtcNow; UpdatedAt = SignedAt.Value; Version++;
    }
    public void Reject(string code)
    {
        if (Status != SigningStatus.Pending) throw new InvalidOperationException("Request already completed");
        FailureCode = code; Status = SigningStatus.Rejected; UpdatedAt = DateTimeOffset.UtcNow; Version++;
    }
}
