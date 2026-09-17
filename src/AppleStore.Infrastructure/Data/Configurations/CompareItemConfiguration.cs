using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class CompareItemConfiguration : IEntityTypeConfiguration<CompareItem>
{
    public void Configure(EntityTypeBuilder<CompareItem> builder)
    {
        builder.HasKey(i => new { i.ListId, i.ProductId });

        builder.HasOne(i => i.CompareList)
            .WithMany()
            .HasForeignKey(i => i.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
