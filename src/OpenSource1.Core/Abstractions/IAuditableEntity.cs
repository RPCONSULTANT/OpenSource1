namespace OpenSource1.Core.Abstractions;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAtUtc { get; set; }
    string CreatedBy { get; set; }
    DateTimeOffset? UpdatedAtUtc { get; set; }
    string? UpdatedBy { get; set; }
}
