using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class PriceChangeConfiguration : IEntityTypeConfiguration<PriceChange>
{
    public void Configure(EntityTypeBuilder<PriceChange> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.OldPrice).HasPrecision(12, 2);
        builder.Property(c => c.NewPrice).HasPrecision(12, 2);
        builder.Property(c => c.Source).HasConversion<int>();
        builder.HasOne(c => c.Variant).WithMany().HasForeignKey(c => c.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.ChangedBy).WithMany().HasForeignKey(c => c.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
        // The history page lists newest first, per variant or overall.
        builder.HasIndex(c => new { c.VariantId, c.ChangedAt });
        builder.HasIndex(c => c.ChangedAt);
    }
}
