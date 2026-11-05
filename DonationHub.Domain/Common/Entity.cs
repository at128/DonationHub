namespace DonationHub.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private set; }

    protected Entity() { }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Entity ID cannot be empty.");

        Id = id;
    }
}