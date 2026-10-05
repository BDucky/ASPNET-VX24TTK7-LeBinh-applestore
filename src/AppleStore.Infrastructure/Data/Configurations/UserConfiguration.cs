using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(120).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(120).IsRequired();
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(120).IsRequired();
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.Property(u => u.Role).HasConversion<int>();
    }
}
