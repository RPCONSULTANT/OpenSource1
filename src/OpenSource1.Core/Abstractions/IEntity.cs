namespace OpenSource1.Core.Abstractions;

public interface IEntity<out TKey>
{
    TKey Id { get; }
}
