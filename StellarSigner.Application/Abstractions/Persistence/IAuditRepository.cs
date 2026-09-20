using StellarSigner.Domain.Entities;
namespace StellarSigner.Application.Abstractions.Persistence;
public interface IAuditRepository { Task AddAsync(AuditRecord record, CancellationToken ct); }
