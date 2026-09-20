using FluentValidation;
using MediatR;
using StellarSigner.Application.Abstractions.Cryptography;
using StellarSigner.Application.Abstractions.Persistence;
using StellarSigner.Domain.Entities;
namespace StellarSigner.Application.Wallets.Commands.DerivePartnerWallet;
public sealed record DerivePartnerWalletCommand(Guid PartnerId) : IRequest<WalletResult>;
public sealed record WalletResult(Guid PartnerId, string PublicKey, long DerivationIndex, string DerivationPath, string Status)
{
    public static WalletResult From(PartnerWallet w) => new(w.PartnerId, w.PublicKey, w.DerivationIndex, w.DerivationPath, w.Status.ToString());
}
public sealed class DerivePartnerWalletValidator : AbstractValidator<DerivePartnerWalletCommand>
{
    public DerivePartnerWalletValidator() { RuleFor(x => x.PartnerId).NotEmpty(); }
}
public sealed class DerivePartnerWalletHandler(IPartnerWalletRepository wallets, IKeyDerivationService derivation)
    : IRequestHandler<DerivePartnerWalletCommand, WalletResult>
{
    public async Task<WalletResult> Handle(DerivePartnerWalletCommand request, CancellationToken ct)
    {
        var result = await wallets.GetOrCreateAsync(request.PartnerId, index =>
        {
            using var key = derivation.Derive(checked((uint)index));
            return key.PublicKey;
        }, ct);
        return WalletResult.From(result);
    }
}
