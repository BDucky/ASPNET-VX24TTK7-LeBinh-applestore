using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.HasKey(v => v.Id);
        // Same length as Orders.VoucherCode, which copies it.
        builder.Property(v => v.Code).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.HasIndex(v => v.Code).IsUnique();
        builder.Property(v => v.DiscountType).HasConversion<int>();
        builder.Property(v => v.DiscountValue).HasPrecision(12, 2);
        builder.Property(v => v.MinOrderAmount).HasPrecision(12, 2);
    }
}

public class VoucherProductConfiguration : IEntityTypeConfiguration<VoucherProduct>
{
    public void Configure(EntityTypeBuilder<VoucherProduct> builder)
    {
        builder.HasKey(vp => new { vp.VoucherId, vp.ProductId });
        builder.HasOne(vp => vp.Voucher).WithMany().HasForeignKey(vp => vp.VoucherId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(vp => vp.Product).WithMany().HasForeignKey(vp => vp.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
