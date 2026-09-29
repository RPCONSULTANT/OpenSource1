namespace OpenSource1.Application.Features.Contabilidad;

/// <summary>
/// Filtros de <c>GET api/contabilidad/movimientos</c>: cuenta y rango de <c>FechaRegistro</c> (ambos extremos incluidos) de la
/// Task 5.5; y, desde la Task 7.4, tipo de documento (<c>int</c> para validar con 400 los valores fuera del enum), número de
/// documento (por contenido, sin distinguir mayúsculas), socio de negocio y registro contable (el asiento completo). Los nuevos
/// van al final y son opcionales: el contrato anterior no cambia.
/// </summary>
public sealed record MovimientoContableSearchCriteria(
    Guid? CuentaContableId,
    DateOnly? Desde,
    DateOnly? Hasta,
    int? TipoDocumento = null,
    string? NumeroDocumento = null,
    Guid? SocioNegocioId = null,
    long? RegistroContableId = null);
