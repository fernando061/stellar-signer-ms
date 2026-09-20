using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StellarSigner.Application.Abstractions.Persistence;
using StellarSigner.Domain.Entities;
namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence.Repositories;
public sealed class PartnerWalletRepository(SignerDbContext db) : IPartnerWalletRepository
{
    public Task<PartnerWallet?> GetAsync(Guid partnerId, CancellationToken ct)
        => db.PartnerWallets.AsNoTracking().SingleOrDefaultAsync(x => x.PartnerId == partnerId, ct);

    public async Task<PartnerWallet> GetOrCreateAsync(Guid partnerId, Func<long, string> derivePublicKey, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var connection = db.Database.GetDbConnection();
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction.GetDbTransaction();
            lockCommand.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@partner_id, 0))";
            var p = lockCommand.CreateParameter(); p.ParameterName = "partner_id"; p.Value = partnerId.ToString("D");
            lockCommand.Parameters.Add(p);
            await lockCommand.ExecuteNonQueryAsync(ct);
        }
        var existing = await db.PartnerWallets.SingleOrDefaultAsync(x => x.PartnerId == partnerId, ct);
        if (existing is not null) { await transaction.CommitAsync(ct); return existing; }
        long index;
        await using (var reserve = connection.CreateCommand())
        {
            reserve.Transaction = transaction.GetDbTransaction();
            reserve.CommandText = "INSERT INTO wallet_derivation_counters (name, next_index) VALUES ('wallets', 0) ON CONFLICT (name) DO NOTHING";
            await reserve.ExecuteNonQueryAsync(ct);
            reserve.CommandText = "UPDATE wallet_derivation_counters SET next_index = next_index + 1 WHERE name = 'wallets' AND next_index < 2147483648 RETURNING next_index - 1";
            index = (long?)await reserve.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Derivation index exhausted");
        }
        var wallet = PartnerWallet.Create(partnerId, derivePublicKey(index), index);
        db.PartnerWallets.Add(wallet);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return wallet;
    }
}
