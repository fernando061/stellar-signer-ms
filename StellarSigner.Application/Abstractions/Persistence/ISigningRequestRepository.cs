using StellarSigner.Domain.Entities;
namespace StellarSigner.Application.Abstractions.Persistence;
public interface ISigningRequestRepository
{
    Task<SigningRequest?> ByRequestIdAsync(Guid requestId, CancellationToken ct);
    Task<SigningRequest?> ByIdempotencyKeyAsync(string key, CancellationToken ct);
    Task AddAsync(SigningRequest request, CancellationToken ct);
}
