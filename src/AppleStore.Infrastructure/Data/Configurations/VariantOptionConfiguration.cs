using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class VariantOptionConfiguration : IEntityTypeConfiguration<VariantOption>
{
    public void Configure(EntityTypeBuilder<VariantOption> builder)
    {
        builder.HasKey(v => new { v.VariantId, v.OptionTypeId });

        builder.HasOne(v => v.Variant)
            .WithMany()
            .HasForeignKey(v => v.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.OptionType)
            .WithMany()
            .HasForeignKey(v => v.OptionTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.OptionValue)
            .WithMany()
            .HasForeignKey(v => v.OptionValueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
