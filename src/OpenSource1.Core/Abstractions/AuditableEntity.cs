namespace OpenSource1.Core.Abstractions;

public abstract class AuditableEntity<TKey> : AggregateRoot<TKey>, IAuditableEntity, ISoftDeletable
    where TKey : notnull
{
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
