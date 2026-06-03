namespace OpenSource1.Core.Abstractions;

public abstract class AggregateRoot<TKey> : IEntity<TKey>, IAggregateRoot
    where TKey : notnull
{
    public TKey Id { get; protected set; } = default!;
}
