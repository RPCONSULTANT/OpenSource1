using MediatR;
using OpenSource1.Application.Common;
using OpenSource1.Application.Features.Inventario.Consultas.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Inventario.Consultas;

/// <summary>
/// Validación común de las vistas de inventario (Review Focus 5): los filtros inválidos son 400 con <c>Campo</c>, nunca 500.
/// Página &lt; 1 y tamaño fuera de [1, 200] se normalizan (<see cref="PageRequest.Normalizar"/>); una página cuyo desplazamiento
/// no cabe en un entero (el OFFSET saldría negativo) es 400.
/// </summary>
internal static class InventarioConsultasValidacion
{
    public static List<Error> Validar(
        DateOnly? desde, DateOnly? hasta, int? tipoMovimiento, int? tipoOrigen, PageRequest paginacion)
    {
        var errores = new List<Error>();
        if (desde is { } d && hasta is { } h && d > h)
        {
            errores.Add(new Error("inventario.rango_fechas_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.", "Desde"));
        }

        if (tipoMovimiento is { } tm && !Definido<TipoMovimientoInventario>(tm))
        {
            errores.Add(new Error(
                "inventario.tipo_movimiento_invalido",
                $"El tipo de movimiento {tm} no es válido (1 Compra, 2 Venta, 3 Ajuste positivo, 4 Ajuste negativo, 5 Transferencia).",
                "TipoMovimiento"));
        }

        if (tipoOrigen is { } to && !Definido<TipoOrigenMovimiento>(to))
        {
            errores.Add(new Error(
                "inventario.tipo_origen_invalido",
                $"El tipo de origen {to} no es válido (1 Diario, 2 Factura de venta, 3 Ajuste de costo, 4 Costo de inventario, 5 Cobro, 99 Migración).",
                "TipoOrigen"));
        }

        ValidarPagina(paginacion, errores);
        return errores;
    }

    public static void ValidarPagina(PageRequest paginacion, List<Error> errores)
    {
        if (PaginacionValidacion.Validar(paginacion, "inventario") is { } error)
        {
            errores.Add(error);
        }
    }

    /// <summary>Los enums del libro son <c>smallint</c>: un valor fuera de <c>short</c> nunca está definido.</summary>
    private static bool Definido<TEnum>(int valor)
        where TEnum : struct, Enum =>
        valor is >= short.MinValue and <= short.MaxValue && Enum.IsDefined(typeof(TEnum), (short)valor);
}

public sealed class ListMovimientosProductoVistaQueryHandler(IInventarioConsultasReadRepository repository)
    : IRequestHandler<ListMovimientosProductoVistaQuery, Result<PagedResult<MovimientoProductoVistaResponse>>>
{
    public async Task<Result<PagedResult<MovimientoProductoVistaResponse>>> Handle(
        ListMovimientosProductoVistaQuery request, CancellationToken cancellationToken)
    {
        var c = request.Criterios;
        var errores = InventarioConsultasValidacion.Validar(c.Desde, c.Hasta, c.TipoMovimiento, c.TipoOrigen, request.Paginacion);
        if (errores.Count > 0)
        {
            return Result<PagedResult<MovimientoProductoVistaResponse>>.Fallo([.. errores]);
        }

        return Result<PagedResult<MovimientoProductoVistaResponse>>.Exito(
            await repository.ListMovimientosProductoAsync(c, request.Paginacion, cancellationToken));
    }
}

public sealed class ListMovimientosValorVistaQueryHandler(IInventarioConsultasReadRepository repository)
    : IRequestHandler<ListMovimientosValorVistaQuery, Result<PagedResult<MovimientoValorVistaResponse>>>
{
    public async Task<Result<PagedResult<MovimientoValorVistaResponse>>> Handle(
        ListMovimientosValorVistaQuery request, CancellationToken cancellationToken)
    {
        var c = request.Criterios;
        var errores = InventarioConsultasValidacion.Validar(c.Desde, c.Hasta, c.TipoMovimiento, c.TipoOrigen, request.Paginacion);
        if (errores.Count > 0)
        {
            return Result<PagedResult<MovimientoValorVistaResponse>>.Fallo([.. errores]);
        }

        return Result<PagedResult<MovimientoValorVistaResponse>>.Exito(
            await repository.ListMovimientosValorAsync(c, request.Paginacion, cancellationToken));
    }
}

public sealed class ListExistenciasVistaQueryHandler(IInventarioConsultasReadRepository repository)
    : IRequestHandler<ListExistenciasVistaQuery, Result<ExistenciasVistaResponse>>
{
    public async Task<Result<ExistenciasVistaResponse>> Handle(ListExistenciasVistaQuery request, CancellationToken cancellationToken)
    {
        var errores = new List<Error>();
        InventarioConsultasValidacion.ValidarPagina(request.Paginacion, errores);
        if (errores.Count > 0)
        {
            return Result<ExistenciasVistaResponse>.Fallo([.. errores]);
        }

        // Fecha de corte por defecto: hoy (mismo criterio de "hoy" que el resto de handlers).
        var criterios = request.Criterios with { Fecha = request.Criterios.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow) };
        return Result<ExistenciasVistaResponse>.Exito(
            await repository.ListExistenciasAsync(criterios, request.Paginacion, cancellationToken));
    }
}
