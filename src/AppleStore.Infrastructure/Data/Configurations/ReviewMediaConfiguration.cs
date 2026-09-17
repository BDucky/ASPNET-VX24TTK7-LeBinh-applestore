using AppleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppleStore.Infrastructure.Data.Configurations;

public class ReviewMediaConfiguration : IEntityTypeConfiguration<ReviewMedia>
{
    public void Configure(EntityTypeBuilder<ReviewMedia> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MediaUrl).HasMaxLength(255).IsRequired();
        builder.Property(m => m.MediaType).HasConversion<int>();

        builder.HasOne(m => m.Review)
            .WithMany()
            .HasForeignKey(m => m.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
