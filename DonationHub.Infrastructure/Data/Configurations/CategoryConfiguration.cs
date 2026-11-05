using DonationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DonationHub.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.NameAr)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(c => c.NameEn)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(c => c.DescriptionAr)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.DescriptionEn)
            .IsRequired()
            .HasMaxLength(500);
        builder.HasIndex(c => c.NameAr).IsUnique();
        builder.HasIndex(c => c.NameEn).IsUnique();
    }
}