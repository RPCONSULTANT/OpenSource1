using OpenSource1.Core.Abstractions;

namespace OpenSource1.Core.Entities;

public abstract class BaseEntity : AuditableEntity<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }
}
