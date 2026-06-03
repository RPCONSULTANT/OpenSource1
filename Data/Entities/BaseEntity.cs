using test.Data.Abstractions;

namespace test.Data.Entities;

public abstract class BaseEntity : AggregateRoot<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
