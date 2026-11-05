using DonationHub.Domain.Constants;
using DonationHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DonationHub.Infrastructure.Data.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    

    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.TitleAr)
            .IsRequired()
            .HasMaxLength(CampaignConstants.MaxTitleLength);

        builder.Property(c => c.TitleEn)
            .IsRequired()
            .HasMaxLength(CampaignConstants.MaxTitleLength);
        
        builder.Property(c => c.DescriptionAr)
            .IsRequired()
            .HasMaxLength(CampaignConstants.MaxDescriptionLength);
        
        builder.Property(c => c.DescriptionEn)
            .IsRequired()
            .HasMaxLength(CampaignConstants.MaxDescriptionLength);
        
        builder.Property(c => c.GoalAmount)
            .HasPrecision(18, 2)
            .IsRequired();
        
        builder.Property(c => c.RaisedAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .IsRequired();
            
        builder.Property(c => c.BannerFileName)
            .HasMaxLength(CampaignConstants.MaxBannerFileNameLength);

        builder.Property(c => c.BannerFileName)
            .HasMaxLength(CampaignConstants.MaxBannerFileNameLength);

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

         builder.Property(c => c.IsDeleted)
            .HasDefaultValue(false);

        builder.HasQueryFilter(c => !c.IsDeleted);

         builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}