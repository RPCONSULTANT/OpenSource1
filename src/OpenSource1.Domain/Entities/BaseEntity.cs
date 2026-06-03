using OpenSource1.Domain.Abstractions;

namespace OpenSource1.Domain.Entities;

public abstract class BaseEntity : AggregateRoot<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
