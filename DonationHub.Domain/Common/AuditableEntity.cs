namespace DonationHub.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected AuditableEntity() { }

    protected AuditableEntity(Guid id) : base(id) { }
}