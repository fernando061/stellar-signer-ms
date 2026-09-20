using FluentValidation;
using MediatR;
using StellarSigner.Application.Abstractions.Persistence;
using StellarSigner.Application.Common;
using StellarSigner.Application.Wallets.Commands.DerivePartnerWallet;
namespace StellarSigner.Application.Wallets.Queries.GetPartnerWallet;
public sealed record GetPartnerWalletQuery(Guid PartnerId) : IRequest<WalletResult>;
public sealed class GetPartnerWalletValidator : AbstractValidator<GetPartnerWalletQuery>
{
    public GetPartnerWalletValidator() { RuleFor(x => x.PartnerId).NotEmpty(); }
}
public sealed class GetPartnerWalletHandler(IPartnerWalletRepository wallets) : IRequestHandler<GetPartnerWalletQuery, WalletResult>
{
    public async Task<WalletResult> Handle(GetPartnerWalletQuery request, CancellationToken ct)
        => WalletResult.From(await wallets.GetAsync(request.PartnerId, ct) ?? throw new SignerException("WALLET_NOT_FOUND", 404));
}
