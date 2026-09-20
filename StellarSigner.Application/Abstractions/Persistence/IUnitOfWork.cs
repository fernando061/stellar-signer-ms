namespace StellarSigner.Application.Abstractions.Persistence;
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<ISigningTransaction> BeginSigningTransactionAsync(string key, Guid requestId, CancellationToken ct);
}
public interface ISigningTransaction : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
