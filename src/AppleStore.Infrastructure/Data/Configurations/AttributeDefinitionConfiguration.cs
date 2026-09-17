using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.ToTable("Attributes");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(120).IsRequired();
        builder.Property(a => a.Unit).HasMaxLength(20);
        builder.Property(a => a.DataType).HasConversion<int>();
    }
}
