namespace OpenSource1.Application.Features.MovimientosCliente;

/// <summary>
/// Filtros de <c>GET api/clientes/estado-cuenta</c> (Task 7.3). <see cref="FechaCorte"/> incluida (el handler la fija a hoy UTC
/// si falta); <see cref="SocioNegocioId"/> = un solo cliente (uno inexistente no es entidad de ruta: página vacía);
/// <see cref="Texto"/> busca por contenido en el código y el nombre comercial; <see cref="SoloConSaldo"/> = solo los clientes con
/// algún documento con restante ≠ 0 a la fecha de corte. Sin filtros: todos los clientes con algún movimiento registrado hasta
/// la fecha de corte.
/// </summary>
public sealed record EstadoCuentaCriterios(
    DateOnly? FechaCorte = null,
    Guid? SocioNegocioId = null,
    string? Texto = null,
    bool SoloConSaldo = false);
