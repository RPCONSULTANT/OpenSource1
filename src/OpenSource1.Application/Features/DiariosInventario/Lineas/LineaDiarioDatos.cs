using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas;

/// <summary>
/// Datos de una línea a validar/calcular, comunes a Create y Update (el PUT es de reemplazo completo:
/// no hay semántica "null = conservar" aquí, a diferencia de otros maestros — una línea de diario es
/// un borrador de un solo paso, no un registro con campos opcionales de larga vida).
/// </summary>
public sealed record LineaDiarioDatos(
    Guid LoteDiarioId,
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    string? NumeroDocumento,
    TipoMovimientoInventario TipoMovimiento,
    Guid ProductoId,
    Guid AlmacenId,
    Guid? AlmacenDestinoId,
    Guid UnidadMedidaId,
    decimal Cantidad,
    decimal? CostoUnitario,
    string? Descripcion);

/// <summary>
/// Resultado de validar y calcular una línea: el factor congelado, el importe de costo, y los
/// códigos/nombres resueltos durante la validación (para construir la respuesta sin una segunda
/// consulta, mismo patrón que <c>ProductoReglas.ResolverReferenciasAsync</c>).
/// </summary>
public sealed record LineaDiarioCalculo(
    decimal CantidadPorUnidadMedida,
    decimal ImporteCosto,
    string ProductoCodigo,
    string ProductoNombre,
    string AlmacenCodigo,
    string? AlmacenDestinoCodigo,
    string UnidadMedidaCodigo);
