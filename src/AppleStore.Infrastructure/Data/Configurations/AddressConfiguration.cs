using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Label).HasMaxLength(60);
        builder.Property(a => a.FullName).HasMaxLength(120).IsRequired();
        builder.Property(a => a.Phone).HasMaxLength(20).IsRequired();
        builder.Property(a => a.AddressLine).HasMaxLength(255).IsRequired();
        builder.Property(a => a.Ward).HasMaxLength(100);
        builder.Property(a => a.District).HasMaxLength(100);
        builder.Property(a => a.City).HasMaxLength(100);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
