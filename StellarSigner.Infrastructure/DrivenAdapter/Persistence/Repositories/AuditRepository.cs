using StellarSigner.Application.Abstractions.Persistence;
using StellarSigner.Domain.Entities;
namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence.Repositories;
public sealed class AuditRepository(SignerDbContext db) : IAuditRepository
{
    public async Task AddAsync(AuditRecord record, CancellationToken ct) => await db.AuditRecords.AddAsync(record, ct);
}
