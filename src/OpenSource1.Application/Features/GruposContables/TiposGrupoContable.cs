using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.GruposContables;

/// <summary>
/// Catálogo de los cinco tipos de grupo contable simple: nombre del segmento de la ruta <c>api/grupos-contables/{tipo}</c>
/// (y del parámetro <c>?tipo=</c> de la página), tabla de Postgres y rótulo en la UI. Es la ÚNICA fuente de los nombres de
/// tabla que el repositorio Dapper interpola en su SQL: el tipo solo puede ser un valor de este catálogo, nunca texto del
/// usuario.
/// </summary>
public static class TiposGrupoContable
{
    public sealed record Descriptor(TipoGrupoContable Tipo, string Ruta, string Tabla, string Nombre, string NombrePlural);

    public static readonly IReadOnlyList<Descriptor> Todos =
    [
        new(TipoGrupoContable.Negocio, "negocio", "GruposNegocio", "Grupo de negocio", "Grupos de negocio"),
        new(TipoGrupoContable.Producto, "producto", "GruposProducto", "Grupo de producto", "Grupos de producto"),
        new(TipoGrupoContable.IvaNegocio, "iva-negocio", "GruposIvaNegocio", "Grupo de IVA de negocio", "Grupos de IVA de negocio"),
        new(TipoGrupoContable.IvaProducto, "iva-producto", "GruposIvaProducto", "Grupo de IVA de producto", "Grupos de IVA de producto"),
        new(TipoGrupoContable.Inventario, "inventario", "GruposInventario", "Grupo de inventario", "Grupos de inventario"),
    ];

    /// <summary>Tipo a partir del segmento de ruta (sin distinguir mayúsculas); <see langword="null"/> si no existe.</summary>
    public static TipoGrupoContable? DesdeRuta(string? ruta) =>
        Todos.FirstOrDefault(d => string.Equals(d.Ruta, ruta?.Trim(), StringComparison.OrdinalIgnoreCase))?.Tipo;

    public static Descriptor De(TipoGrupoContable tipo) =>
        Todos.FirstOrDefault(d => d.Tipo == tipo) ?? throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de grupo contable desconocido.");

    public static bool EsValido(TipoGrupoContable tipo) => Todos.Any(d => d.Tipo == tipo);

    /// <summary>Tipo de entidad (tabla) de cada tipo de grupo.</summary>
    public static Type TipoEntidad(TipoGrupoContable tipo) => tipo switch
    {
        TipoGrupoContable.Negocio => typeof(GrupoNegocio),
        TipoGrupoContable.Producto => typeof(GrupoProducto),
        TipoGrupoContable.IvaNegocio => typeof(GrupoIvaNegocio),
        TipoGrupoContable.IvaProducto => typeof(GrupoIvaProducto),
        TipoGrupoContable.Inventario => typeof(GrupoInventario),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de grupo contable desconocido.")
    };
}
