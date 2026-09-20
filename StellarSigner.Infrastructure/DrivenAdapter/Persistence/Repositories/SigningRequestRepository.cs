using Microsoft.EntityFrameworkCore;
using StellarSigner.Application.Abstractions.Persistence;
using StellarSigner.Domain.Entities;
namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence.Repositories;
public sealed class SigningRequestRepository(SignerDbContext db) : ISigningRequestRepository
{
    public Task<SigningRequest?> ByRequestIdAsync(Guid id, CancellationToken ct)
        => db.SigningRequests.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == id, ct);
    public Task<SigningRequest?> ByIdempotencyKeyAsync(string key, CancellationToken ct)
        => db.SigningRequests.SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
    public async Task AddAsync(SigningRequest request, CancellationToken ct) => await db.SigningRequests.AddAsync(request, ct);
}
