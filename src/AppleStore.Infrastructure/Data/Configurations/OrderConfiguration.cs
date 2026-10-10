using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasConversion<int>();
        builder.Property(o => o.PaymentStatus).HasConversion<int>();
        builder.Property(o => o.Subtotal).HasPrecision(12, 2);
        builder.Property(o => o.DiscountAmount).HasPrecision(12, 2);
        builder.Property(o => o.ShippingFee).HasPrecision(12, 2);
        builder.Property(o => o.TotalAmount).HasPrecision(12, 2);
        builder.Property(o => o.VoucherCode).HasMaxLength(40);
        builder.Property(o => o.ReceiverName).HasMaxLength(120).IsRequired();
        builder.Property(o => o.Phone).HasMaxLength(20).IsRequired();
        builder.Property(o => o.AddressLine).HasMaxLength(255).IsRequired();
        builder.Property(o => o.Ward).HasMaxLength(100);
        builder.Property(o => o.District).HasMaxLength(100);
        builder.Property(o => o.City).HasMaxLength(100);
        builder.Property(o => o.Note).HasMaxLength(400);

        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(o => o.Channel).HasConversion<int>();
        builder.HasOne(o => o.SoldBy)
            .WithMany()
            .HasForeignKey(o => o.SoldByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(o => o.FormKey).IsUnique();
    }
}
