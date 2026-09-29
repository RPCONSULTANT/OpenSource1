using OpenSource1.Core.Common;

namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>
/// Escritor ÚNICO del libro contable (<c>MovimientosContables</c> + <c>RegistrosContables</c>, Task 5.5), mismo papel que
/// <c>IRegistroMovimientosInventario</c> en inventario. El batch de costo (Task 5.6) y la facturación (Fase 6) escriben en la
/// contabilidad solo a través de este servicio.
/// </summary>
public interface IRegistroContable
{
    /// <summary>
    /// Requiere transacción activa. No hace commit. Rechaza sin escribir: sin líneas, importe 0 en una línea, &gt;4 decimales,
    /// cuenta inexistente/no Posteo/bloqueada, SUM(Importe) &lt;&gt; 0 (<c>contabilidad.asiento_descuadrado</c> con el
    /// descuadre). Tras insertar vuelve a comprobar el cuadre con <c>SELECT SUM</c>; si no es 0 lanza una excepción (el
    /// llamador no debe confirmar: su transacción se deshace entera). Numera con la serie <c>CONTAB</c> (sin huecos: un
    /// fallo no consume número porque todo se deshace con la transacción del llamador).
    /// </summary>
    Task<Result<AsientoRegistrado>> RegistrarAsync(AsientoContable asiento, CancellationToken ct = default);
}
