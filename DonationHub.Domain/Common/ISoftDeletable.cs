namespace DonationHub.Domain.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
}