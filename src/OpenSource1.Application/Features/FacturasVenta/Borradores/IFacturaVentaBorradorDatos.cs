namespace OpenSource1.Application.Features.FacturasVenta.Borradores;

/// <summary>Escrituras masivas del borrador por SQL directo (Dapper, en la transacción del llamador).</summary>
public interface IFacturaVentaBorradorDatos
{
    /// <summary>
    /// Borrado lógico de TODAS las líneas vivas del borrador (mismas columnas que el borrado lógico de <c>UnitOfWork</c>).
    /// Requiere transacción activa. Devuelve cuántas se borraron. Lo usan el borrado del borrador y el posteo (Task 6.4).
    /// </summary>
    Task<int> BorrarLineasAsync(Guid facturaVentaBorradorId, string usuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>FOR SHARE</c> de los socios indicados (vender-a y facturar-a), en orden de Id y sin repetir, ANTES de validarlos: un
    /// borrado concurrente del socio (que toma su fila <c>FOR UPDATE</c> antes de evaluar el uso) espera al commit del borrador y
    /// lo ve, o el borrador espera al borrado y ve el socio borrado. Requiere transacción activa. Orden global: documento/borrador
    /// → socios.
    /// </summary>
    Task BloquearSociosAsync(IEnumerable<Guid> socioNegocioIds, CancellationToken cancellationToken = default);
}
