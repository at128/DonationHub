using DonationHub.Domain.Common;
using DonationHub.Domain.Constants;
using DonationHub.Domain.Enums;

namespace DonationHub.Domain.Entities;

public class Campaign: AuditableEntity, ISoftDeletable
{
    public decimal GoalAmount {get; private set;}
    public decimal RaisedAmount {get; private set;}
    public DateTime EndDate {get; private set;}
    public CampaignStatus Status {get; private set;}

    public string TitleAr { get; private set; } = null!;
    public string TitleEn { get; private set; } = null!;
    public string DescriptionAr { get; private set; } = null!;
    public string DescriptionEn { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public bool IsDeleted {get; private set;}
    public DateTime? DeletedAtUtc { get; private set; }
    public string? BannerFileName { get; private set; }
    public byte[]? RowVersion { get; private set; }

    private Campaign() { }

    private Campaign(Guid id) : base(id)
    {
    }

    public static Campaign Create(decimal goalAmount, DateTime endDate, DateTime nowUtc,string titleAr,string titleEn,string descriptionAr,string descriptionEn,Guid categoryId,Guid createdByUserId)
    {
        
        ValidateGoalAmount(goalAmount);

        ValidateDate(endDate,nowUtc);

        titleAr = ValidateTitle(titleAr);
        titleEn = ValidateTitle(titleEn);

        descriptionAr = ValidateDescription(descriptionAr);
        descriptionEn = ValidateDescription(descriptionEn);

        ValidateId(categoryId, nameof(categoryId));
        ValidateId(createdByUserId, nameof(createdByUserId));

        return new Campaign(Guid.CreateVersion7())
        {
            GoalAmount = goalAmount,
            RaisedAmount = 0,
            EndDate = endDate,
            Status = CampaignStatus.Active,
            TitleAr = titleAr,
            TitleEn = titleEn,
            DescriptionAr = descriptionAr,
            DescriptionEn = descriptionEn,
            CategoryId = categoryId,
            CreatedByUserId = createdByUserId
        };
    }

    private static void ValidateId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Id cannot be empty.",
                parameterName);
        }
    }
    private static string ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be null or empty.");
        }

        title=title.Trim();
        
        if (title.Length > CampaignConstants.MaxTitleLength)
        {
            throw new ArgumentException("Title cannot exceed 200 characters.");
        }

        return title;
    }
    
    private static string ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be null or empty.");
        }
        description=description.Trim();
        if (description.Length > CampaignConstants.MaxDescriptionLength)
        {
            throw new ArgumentException("Description cannot exceed 1000 characters.");
        }
        return description;
    }
    
    public void ChangeGoal(decimal newGoalAmount)
    {
        
        ValidateGoalAmount(newGoalAmount);


        if (newGoalAmount < RaisedAmount)
        {
            throw new ArgumentException(
                "New goal amount must be greater than or equal  the raised amount.");
        }

        GoalAmount = newGoalAmount;
    }

    private static void ValidateGoalAmount(decimal goalAmount)
    {
        if (goalAmount <= 0)
        {
            throw new ArgumentException("Goal amount must be greater than zero.");
        }
    }

    public void ChangeEndDate(DateTime newEndDate,DateTime nowUtc)
    {
        ValidateDate(newEndDate,nowUtc);

        EndDate = newEndDate;
    }

    private static void ValidateDate(DateTime newEndTime,DateTime nowUtc)
    {
        if (newEndTime.Kind != DateTimeKind.Utc ||
            nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Dates must be in UTC.");
        }

        if (newEndTime <= nowUtc)
        {
            throw new ArgumentException("End date must be in the future.");
        }
    }
    private void MarkCompleted()
    {
        
        if(Status == CampaignStatus.Completed)
        {
            throw new InvalidOperationException("Campaign is already marked as completed.");
        }

        if (!HasReachedGoal())
        {
            throw new InvalidOperationException("Raised amount has not reached the goal amount yet.");
        }

        if(Status != CampaignStatus.Active)
        {
            throw new InvalidOperationException("Only active campaigns can be marked as completed.");
        }
        Status = CampaignStatus.Completed;
    }

    private bool HasReachedGoal()
    {
        return RaisedAmount >= GoalAmount;
    }

    public void RecordSuccessfulDonation(decimal amount)
    {
        if (Status != CampaignStatus.Active)
        {
            throw new InvalidOperationException("Donations can only be recorded for active campaigns.");
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Donation amount must be greater than zero.");
        }

        RaisedAmount += amount;

        if (HasReachedGoal())
        {
            MarkCompleted();
        }
    }

    public void EnsureCanAcceptDonation(DateTime nowUtc)
    {
        if (Status != CampaignStatus.Active)
        {
            throw new InvalidOperationException("Donations can only be accepted for active campaigns.");
        }

        if(nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Current time must be in UTC.");
        }
        if(nowUtc >= EndDate)
        {
            throw new InvalidOperationException("Campaign has expired and can no longer accept donations.");
        }
    }
    public void MarkExpired(DateTime nowUtc)
    {
        if (Status != CampaignStatus.Active)
        {
            throw new InvalidOperationException("Only active campaigns can be marked as expired.");
        }
        if (HasReachedGoal())
        {
            throw new InvalidOperationException(
            "Campaign has already reached its goal.");
        }

        if(nowUtc.Kind != DateTimeKind.Utc
        || EndDate.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Dates must be in UTC.");
        }


        if(EndDate > nowUtc)
        {
            throw new InvalidOperationException("Campaign has not yet reached its end date.");
        }
        
        
        
        Status = CampaignStatus.Expired;
    }

    
    public void ChangeBanner(string bannerFileName)
    {
        if (string.IsNullOrWhiteSpace(bannerFileName))
        {
            throw new ArgumentException(
                "Banner file name cannot be empty.",
                nameof(bannerFileName));
        }

        bannerFileName = bannerFileName.Trim();

        if (bannerFileName.Length > CampaignConstants.MaxBannerFileNameLength)
        {
            throw new ArgumentException(
                $"Banner file name cannot exceed {CampaignConstants.MaxBannerFileNameLength} characters.",
                nameof(bannerFileName));
        }

        BannerFileName = bannerFileName;
    }

    public void RemoveBanner()
    {
        BannerFileName = null;
    }
}
