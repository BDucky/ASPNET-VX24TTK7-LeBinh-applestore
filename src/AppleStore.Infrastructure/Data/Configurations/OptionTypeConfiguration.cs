using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class OptionTypeConfiguration : IEntityTypeConfiguration<OptionType>
{
    public void Configure(EntityTypeBuilder<OptionType> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Code).HasMaxLength(30).IsRequired();
    }
}
