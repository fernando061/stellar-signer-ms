using StellarSigner.Domain.Enums;
namespace StellarSigner.Domain.Entities;
public sealed class AuditRecord
{
    private AuditRecord() { }
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = null!;
    public Guid RequestId { get; private set; }
    public Guid RemittanceId { get; private set; }
    public Guid PartnerId { get; private set; }
    public string? PublicKey { get; private set; }
    public string? TransactionHash { get; private set; }
    public SigningOperationType OperationType { get; private set; }
    public decimal? Amount { get; private set; }
    public string? AssetCode { get; private set; }
    public string? Destination { get; private set; }
    public string Result { get; private set; } = null!;
    public string? FailureCode { get; private set; }
    public string CallerId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public static AuditRecord ForSigning(Guid requestId, Guid remittanceId, Guid partnerId, string? publicKey,
        string? txHash, SigningOperationType operation, decimal? amount, string? assetCode, string? destination,
        string result, string? failureCode, string callerId) => new()
    {
        EventType = "SIGN_TRANSACTION", RequestId = requestId, RemittanceId = remittanceId, PartnerId = partnerId,
        PublicKey = publicKey, TransactionHash = txHash, OperationType = operation, Amount = amount,
        AssetCode = assetCode, Destination = destination, Result = result, FailureCode = failureCode,
        CallerId = callerId, CreatedAt = DateTimeOffset.UtcNow
    };
}
