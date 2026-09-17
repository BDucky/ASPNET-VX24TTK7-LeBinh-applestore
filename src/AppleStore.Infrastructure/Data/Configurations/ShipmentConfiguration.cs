using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Carrier).HasMaxLength(60).IsRequired();
        builder.Property(s => s.TrackingNo).HasMaxLength(80);
        builder.Property(s => s.Status).HasConversion<int>();
        builder.Property(s => s.Fee).HasPrecision(12, 2);

        builder.HasOne(s => s.Order)
            .WithMany()
            .HasForeignKey(s => s.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
