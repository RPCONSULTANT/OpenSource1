using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.TestInfrastructure;

internal static class EntidadConId
{
    /// <summary>Fija el Id (setter protegido de <see cref="AggregateRoot{TKey}"/>) para simular filas semilla con Id fijo.</summary>
    public static T ConId<T>(this T entidad, Guid id) where T : BaseEntity
    {
        typeof(AggregateRoot<Guid>).GetProperty(nameof(AggregateRoot<Guid>.Id))!.SetValue(entidad, id);
        return entidad;
    }
}
