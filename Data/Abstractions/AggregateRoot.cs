namespace test.Data.Abstractions;

public abstract class AggregateRoot<TKey> : IEntity<TKey>, IAggregateRoot
    where TKey : notnull
{
    public TKey Id { get; protected set; } = default!;
}
