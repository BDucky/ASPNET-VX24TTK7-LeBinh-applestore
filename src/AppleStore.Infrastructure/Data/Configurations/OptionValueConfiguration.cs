using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class OptionValueConfiguration : IEntityTypeConfiguration<OptionValue>
{
    public void Configure(EntityTypeBuilder<OptionValue> builder)
    {
        builder.HasKey(ov => ov.Id);
        builder.Property(ov => ov.Value).HasMaxLength(60).IsRequired();

        builder.HasOne(ov => ov.OptionType)
            .WithMany()
            .HasForeignKey(ov => ov.OptionTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
