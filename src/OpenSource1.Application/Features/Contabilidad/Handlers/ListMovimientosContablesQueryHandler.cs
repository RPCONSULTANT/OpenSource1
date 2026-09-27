using MediatR;
using OpenSource1.Application.Common;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Application.Features.Contabilidad.Queries;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Contabilidad.Handlers;

internal static class ContabilidadErrores
{
    public static Error RangoFechasInvalido() =>
        new("contabilidad.rango_fechas_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.", "Desde");
}

public sealed class ListMovimientosContablesQueryHandler(IContabilidadReadRepository readRepository)
    : IRequestHandler<ListMovimientosContablesQuery, Result<PagedResult<MovimientoContableResponse>>>
{
    public Task<Result<PagedResult<MovimientoContableResponse>>> Handle(
        ListMovimientosContablesQuery request, CancellationToken cancellationToken)
    {
        // Review Focus 5 (Task 7.4): filtros inválidos → 400 con el campo (todos a la vez), nunca 500.
        var errores = new List<Error>();
        if (request.Search is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            errores.Add(ContabilidadErrores.RangoFechasInvalido());
        }

        if (request.Search.TipoDocumento is { } tipo
            && !(tipo is >= short.MinValue and <= short.MaxValue && Enum.IsDefined(typeof(TipoDocumentoContable), (short)tipo)))
        {
            errores.Add(new Error(
                "contabilidad.tipo_documento_invalido",
                $"El tipo de documento {tipo} no es válido (0 Ninguno, 1 Costo de inventario, 2 Factura de venta, 3 Cobro).",
                "TipoDocumento"));
        }

        // Una página cuyo OFFSET desborda int daba 500 en Postgres (hueco heredado de la Task 5.5).
        if (PaginacionValidacion.Validar(request.Paginacion, "contabilidad") is { } errorPagina)
        {
            errores.Add(errorPagina);
        }

        return errores.Count > 0
            ? Task.FromResult(Result<PagedResult<MovimientoContableResponse>>.Fallo([.. errores]))
            : readRepository.ListMovimientosAsync(request.Search, request.Paginacion, cancellationToken);
    }
}
