using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using StellarSigner.Domain.Entities;
using StellarSigner.Application.Abstractions.Persistence;

namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence;

public sealed class SignerDbContext(DbContextOptions<SignerDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<PartnerWallet> PartnerWallets => Set<PartnerWallet>();
    public DbSet<WalletDerivationCounter> WalletDerivationCounters => Set<WalletDerivationCounter>();
    public DbSet<SigningRequest> SigningRequests => Set<SigningRequest>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    public async Task<ISigningTransaction> BeginSigningTransactionAsync(string key, Guid requestId, CancellationToken ct)
    {
        var transaction = await Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await using (var command = Database.GetDbConnection().CreateCommand())
        {
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key, 1))";
            var p = command.CreateParameter(); p.ParameterName = "key"; p.Value = key;
            command.Parameters.Add(p);
            await command.ExecuteNonQueryAsync(ct);
            command.Parameters.Clear();
            command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@request_id, 2))";
            p = command.CreateParameter(); p.ParameterName = "request_id"; p.Value = requestId.ToString("D");
            command.Parameters.Add(p);
            await command.ExecuteNonQueryAsync(ct);
        }
        return new SigningTransaction(transaction);
    }
    private sealed class SigningTransaction(IDbContextTransaction inner) : ISigningTransaction
    {
        public Task CommitAsync(CancellationToken ct) => inner.CommitAsync(ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SignerDbContext).Assembly);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
                property.SetColumnName(Snake(property.Name));
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAppendOnlyAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceAppendOnlyAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    private void EnforceAppendOnlyAudit()
    {
        if (ChangeTracker.Entries<AuditRecord>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit records are append-only");
    }
    private static string Snake(string name) => Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
