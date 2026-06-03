namespace OpenSource1.Core.Abstractions;

public abstract class AuditableEntity<TKey> : AggregateRoot<TKey>, IAuditableEntity
    where TKey : notnull
{
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
