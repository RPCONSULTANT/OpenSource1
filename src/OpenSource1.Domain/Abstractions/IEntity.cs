namespace OpenSource1.Domain.Abstractions;

public interface IEntity<out TKey>
{
    TKey Id { get; }
}
