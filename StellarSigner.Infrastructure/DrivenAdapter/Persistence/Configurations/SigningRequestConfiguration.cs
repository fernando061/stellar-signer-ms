using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StellarSigner.Domain.Entities;
namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence.Configurations;
public sealed class SigningRequestConfiguration : IEntityTypeConfiguration<SigningRequest>
{
    public void Configure(EntityTypeBuilder<SigningRequest> b)
    {
        b.ToTable("signing_requests", t => t.HasCheckConstraint("ck_signing_requests_amount", "amount > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        b.Property(x => x.RequestId).HasColumnType("uuid").IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(160).IsRequired();
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.PublicKey).HasMaxLength(56);
        b.Property(x => x.TransactionHash).HasMaxLength(64);
        b.Property(x => x.UnsignedXdrHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.SignedXdrHash).HasMaxLength(64);
        b.Property(x => x.SignedXdr).HasColumnType("text");
        b.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.AssetCode).HasMaxLength(12).IsRequired();
        b.Property(x => x.AssetIssuer).HasMaxLength(56).IsRequired();
        b.Property(x => x.Destination).HasMaxLength(56).IsRequired();
        b.Property(x => x.Amount).HasPrecision(20, 7).IsRequired();
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.Property(x => x.FailureReason).HasMaxLength(256);
        b.Property(x => x.FailureStatusCode);
        b.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0u);
        b.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => x.RemittanceId);
    }
}
