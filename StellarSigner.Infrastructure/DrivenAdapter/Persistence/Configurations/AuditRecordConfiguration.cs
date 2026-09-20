using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StellarSigner.Domain.Entities;
namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence.Configurations;
public sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> b)
    {
        b.ToTable("audit_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        b.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        b.Property(x => x.PublicKey).HasMaxLength(56);
        b.Property(x => x.TransactionHash).HasMaxLength(64);
        b.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Amount).HasPrecision(20, 7);
        b.Property(x => x.AssetCode).HasMaxLength(12);
        b.Property(x => x.Destination).HasMaxLength(56);
        b.Property(x => x.Result).HasMaxLength(24).IsRequired();
        b.Property(x => x.FailureCode).HasMaxLength(64);
        b.Property(x => x.CallerId).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.RequestId);
        b.HasIndex(x => x.CreatedAt);
    }
}
