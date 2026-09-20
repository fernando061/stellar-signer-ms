using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StellarSigner.Application.Wallets.Commands.DerivePartnerWallet;
using StellarSigner.Application.Wallets.Queries.GetPartnerWallet;
namespace StellarSigner.Api.Controllers;
[ApiController]
[Authorize(Policy = "StellarSigner.Execute")]
[Route("api/internal/v1/wallets")]
public sealed class WalletsController(IMediator mediator) : ControllerBase
{
    public sealed record DeriveWalletRequest(Guid PartnerId);
    [HttpPost]
    public async Task<ActionResult<WalletResult>> Derive([FromBody] DeriveWalletRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new DerivePartnerWalletCommand(body.PartnerId), ct));
    [HttpGet("{partnerId:guid}")]
    public async Task<ActionResult<WalletResult>> Get(Guid partnerId, CancellationToken ct)
        => Ok(await mediator.Send(new GetPartnerWalletQuery(partnerId), ct));
}
