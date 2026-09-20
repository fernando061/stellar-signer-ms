using FluentValidation;
using MediatR;
using StellarSigner.Application.Abstractions.Persistence;
using StellarSigner.Application.Common;
namespace StellarSigner.Application.Signatures.Queries.GetSigningRequest;
public sealed record GetSigningRequestQuery(Guid RequestId) : IRequest<GetSigningRequestResult>;
public sealed record GetSigningRequestResult(Guid RequestId, string? TransactionHash, string Status, DateTimeOffset? SignedAt, string? FailureCode);
public sealed class GetSigningRequestValidator : AbstractValidator<GetSigningRequestQuery>
{
    public GetSigningRequestValidator() { RuleFor(x => x.RequestId).NotEmpty(); }
}
public sealed class GetSigningRequestHandler(ISigningRequestRepository requests) : IRequestHandler<GetSigningRequestQuery, GetSigningRequestResult>
{
    public async Task<GetSigningRequestResult> Handle(GetSigningRequestQuery query, CancellationToken ct)
    {
        var r = await requests.ByRequestIdAsync(query.RequestId, ct) ?? throw new SignerException("SIGNING_REQUEST_NOT_FOUND", 404);
        return new GetSigningRequestResult(r.RequestId, r.TransactionHash, r.Status.ToString(), r.SignedAt, r.FailureCode);
    }
}
