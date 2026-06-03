using OpenSource1.Core.Abstractions;

namespace OpenSource1.Core.Entities;

public abstract class BaseEntity : AggregateRoot<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
