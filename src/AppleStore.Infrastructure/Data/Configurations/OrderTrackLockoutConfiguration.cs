using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class OrderTrackLockoutConfiguration : IEntityTypeConfiguration<OrderTrackLockout>
{
    public void Configure(EntityTypeBuilder<OrderTrackLockout> builder)
    {
        builder.HasKey(l => l.Id);
        builder.HasIndex(l => l.OrderId).IsUnique();
        builder.HasOne<Order>().WithMany().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
