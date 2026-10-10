using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class StockReceiptConfiguration : IEntityTypeConfiguration<StockReceipt>
{
    public void Configure(EntityTypeBuilder<StockReceipt> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Supplier).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(400);
        builder.HasIndex(r => r.FormKey).IsUnique();
        builder.HasIndex(r => r.CreatedAt);
        builder.HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class StockReceiptLineConfiguration : IEntityTypeConfiguration<StockReceiptLine>
{
    public void Configure(EntityTypeBuilder<StockReceiptLine> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.UnitCost).HasPrecision(12, 2);
        builder.HasOne(l => l.Receipt).WithMany(r => r.Lines).HasForeignKey(l => l.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        // A receipt is a record of goods received: the variant cannot be deleted under it.
        builder.HasOne(l => l.Variant).WithMany().HasForeignKey(l => l.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.ReceiptId, l.VariantId }).IsUnique();
    }
}
