using DonationHub.Domain.Common;

namespace DonationHub.Domain.Entities;

public class RefreshToken :Entity
{
    public Guid UserId {get; private set;}
    public string TokenHash{get; private set;}=null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    
    private RefreshToken() { }

    private RefreshToken(Guid id) : base(id)
    {
    }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime createdAt,
        DateTime expiresAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Invalid user ID.");

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.");

        if (createdAt.Kind != DateTimeKind.Utc ||
            expiresAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Dates must be in UTC.");

        if (expiresAt <= createdAt)
            throw new ArgumentException(
                "Expiration must be after creation.");

        return new RefreshToken(Guid.CreateVersion7())
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    public bool IsActive(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Date must be in UTC.");

        return RevokedAt is null && nowUtc < ExpiresAt;
    }

    public void Revoke(
        DateTime nowUtc,
        Guid? replacedByTokenId = null)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Date must be in UTC.");

        if (RevokedAt is not null)
            throw new InvalidOperationException(
                "Token is already revoked.");

        if (replacedByTokenId == Guid.Empty)
            throw new ArgumentException(
                "Replacement token ID cannot be empty.");

        RevokedAt = nowUtc;
        ReplacedByTokenId = replacedByTokenId;
    }
}