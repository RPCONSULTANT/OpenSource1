using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FacturasVenta.Copia;

/// <summary>
/// Crea un borrador nuevo a partir de una factura POSTEADA (spec no-series): número de la serie de borradores configurada, fecha de
/// hoy y serie de registro configurada; cabecera con el cliente, el facturar-a, el almacén, la moneda y la descripción de la factura, y
/// el nombre, el término de pago y los grupos ACTUALES del cliente; líneas (producto, cuenta contable y comentario) con producto o
/// cuenta, unidad, cantidad, precio y descuento. Una línea que ya no pasa la validación actual (producto borrado o bloqueado…) se omite
/// con aviso; un almacén de la factura que ya no vale se sustituye por el predeterminado con aviso; un cliente bloqueado (cualquier
/// bloqueo) o borrado impide la copia. Atómico.
/// </summary>
public sealed record CopiarFacturaABorradorCommand(string FacturaVentaNumero) : IRequest<Result<CopiaFacturaResponse>>;

/// <summary>Borrador creado y avisos (líneas omitidas, almacén sustituido). Vacío = copia completa.</summary>
public sealed record CopiaFacturaResponse(Guid BorradorId, string Numero, IReadOnlyList<string> Avisos);
