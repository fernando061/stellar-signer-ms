using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StellarSigner.Domain.Entities;
namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence.Configurations;
public sealed class PartnerWalletConfiguration : IEntityTypeConfiguration<PartnerWallet>
{
    public void Configure(EntityTypeBuilder<PartnerWallet> b)
    {
        b.ToTable("partner_wallets", t => t.HasCheckConstraint("ck_partner_wallets_derivation_index", "derivation_index >= 0 AND derivation_index < 2147483648"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        b.Property(x => x.PartnerId).HasColumnType("uuid").IsRequired();
        b.Property(x => x.PublicKey).HasMaxLength(56).IsRequired();
        b.Property(x => x.DerivationPath).HasMaxLength(40).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0u);
        b.HasIndex(x => x.PartnerId).IsUnique();
        b.HasIndex(x => x.PublicKey).IsUnique();
        b.HasIndex(x => x.DerivationIndex).IsUnique();
    }
}
