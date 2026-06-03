namespace test.Data.Abstractions;

public interface IEntity<out TKey>
{
    TKey Id { get; }
}
