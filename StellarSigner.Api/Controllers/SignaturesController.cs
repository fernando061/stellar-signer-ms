using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StellarSigner.Application.Signatures.Commands.SignTransaction;
using StellarSigner.Application.Signatures.Queries.GetSigningRequest;
using StellarSigner.Domain.Enums;
namespace StellarSigner.Api.Controllers;
[ApiController]
[Authorize(Policy = "StellarSigner.Execute")]
[Route("api/internal/v1/signatures")]
public sealed class SignaturesController(IMediator mediator) : ControllerBase
{
    public sealed record SignRequest(Guid RequestId, Guid RemittanceId, Guid PartnerId, SigningOperationType OperationType,
        string UnsignedTransactionXdr, ExpectedTransaction ExpectedTransaction);
    [HttpPost]
    public async Task<ActionResult<SignTransactionResult>> Sign([FromBody] SignRequest body, [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        var caller = User.FindFirstValue("client_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var command = new SignTransactionCommand(body.RequestId, body.RemittanceId, body.PartnerId,
            body.OperationType, body.UnsignedTransactionXdr, body.ExpectedTransaction, key, caller);
        return Ok(await mediator.Send(command, ct));
    }
    [HttpGet("{requestId:guid}")]
    public async Task<ActionResult<GetSigningRequestResult>> Get(Guid requestId, CancellationToken ct)
        => Ok(await mediator.Send(new GetSigningRequestQuery(requestId), ct));
}
