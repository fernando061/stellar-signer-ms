using StellarSigner.Domain.Entities;
namespace StellarSigner.Application.Abstractions.Persistence;
public interface IPartnerWalletRepository
{
    Task<PartnerWallet?> GetAsync(Guid partnerId, CancellationToken ct);
    Task<PartnerWallet> GetOrCreateAsync(Guid partnerId, Func<long, string> derivePublicKey, CancellationToken ct);
}
