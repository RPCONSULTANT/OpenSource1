namespace OpenSource1.Application.Features.FacturasVenta.Borradores;

/// <summary>Escrituras masivas del borrador por SQL directo (Dapper, en la transacción del llamador).</summary>
public interface IFacturaVentaBorradorDatos
{
    /// <summary>
    /// Borrado lógico de TODAS las líneas vivas del borrador (mismas columnas que el borrado lógico de <c>UnitOfWork</c>).
    /// Requiere transacción activa. Devuelve cuántas se borraron. Lo usan el borrado del borrador y el posteo (Task 6.4).
    /// </summary>
    Task<int> BorrarLineasAsync(Guid facturaVentaBorradorId, string usuario, CancellationToken cancellationToken = default);
}
