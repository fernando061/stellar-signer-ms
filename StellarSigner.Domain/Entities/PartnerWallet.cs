using StellarSigner.Domain.Enums;
using StellarSigner.Domain.ValueObjects;
namespace StellarSigner.Domain.Entities;
public sealed class PartnerWallet
{
    private PartnerWallet() { }
    public Guid Id { get; private set; }
    public Guid PartnerId { get; private set; }
    public string PublicKey { get; private set; } = null!;
    public long DerivationIndex { get; private set; }
    public string DerivationPath { get; private set; } = null!;
    public WalletStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public static PartnerWallet Create(Guid partnerId, string publicKey, long index)
    {
        if (partnerId == Guid.Empty || index < 0 || index >= 0x80000000L) throw new ArgumentException("Invalid wallet identity");
        _ = new StellarPublicKey(publicKey);
        return new PartnerWallet { PartnerId = partnerId, PublicKey = publicKey, DerivationIndex = index,
            DerivationPath = new DerivationPath((uint)index).Value, Status = WalletStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
    }
    public void Suspend() { Status = WalletStatus.Suspended; UpdatedAt = DateTimeOffset.UtcNow; Version++; }
}
