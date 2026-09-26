using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.GruposContables;

/// <summary>
/// Punto único que traduce el <see cref="TipoGrupoContable"/> (dato) al tipo de entidad (tabla) de EF: cada handler genérico
/// escribe su lógica UNA sola vez como <see cref="IAccion{T}"/> genérica sobre <c>TGrupo</c> y este despacho la ejecuta con la
/// entidad correcta. Así los cinco grupos simples comparten Create/Update/Delete sin duplicar código ni usar reflexión.
/// </summary>
internal static class GrupoContableDespacho
{
    public interface IAccion<T>
    {
        Task<T> EjecutarAsync<TGrupo>() where TGrupo : GrupoContable, new();
    }

    public static Task<T> EjecutarAsync<T>(TipoGrupoContable tipo, IAccion<T> accion) => tipo switch
    {
        TipoGrupoContable.Negocio => accion.EjecutarAsync<GrupoNegocio>(),
        TipoGrupoContable.Producto => accion.EjecutarAsync<GrupoProducto>(),
        TipoGrupoContable.IvaNegocio => accion.EjecutarAsync<GrupoIvaNegocio>(),
        TipoGrupoContable.IvaProducto => accion.EjecutarAsync<GrupoIvaProducto>(),
        TipoGrupoContable.Inventario => accion.EjecutarAsync<GrupoInventario>(),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de grupo contable desconocido.")
    };

    public static Error ErrorTipoInvalido() => new(
        "grupo_contable.tipo_invalido", "El tipo de grupo contable no es válido.", "Tipo");

    public static Error ErrorNoEncontrado(TipoGrupoContable tipo) => new(
        "grupo_contable.no_encontrado", $"No se encontró el {TiposGrupoContable.De(tipo).Nombre.ToLowerInvariant()} solicitado.", "Id");

    public static GrupoContableResponse ToResponse(TipoGrupoContable tipo, GrupoContable x, long xmin) => new()
    {
        Id = x.Id,
        Tipo = tipo,
        Codigo = x.Codigo,
        Descripcion = x.Descripcion,
        Xmin = xmin,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
