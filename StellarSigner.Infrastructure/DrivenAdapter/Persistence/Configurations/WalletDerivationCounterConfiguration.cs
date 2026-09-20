using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StellarSigner.Domain.Entities;
namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence.Configurations;
public sealed class WalletDerivationCounterConfiguration : IEntityTypeConfiguration<WalletDerivationCounter>
{
    public void Configure(EntityTypeBuilder<WalletDerivationCounter> b)
    {
        b.ToTable("wallet_derivation_counters", t => {
            t.HasCheckConstraint("ck_wallet_derivation_counters_singleton", "name = 'wallets'");
            t.HasCheckConstraint("ck_wallet_derivation_counters_next_index", "next_index >= 0 AND next_index <= 2147483648");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        b.Property(x => x.Name).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.NextIndex).IsRequired();
    }
}
