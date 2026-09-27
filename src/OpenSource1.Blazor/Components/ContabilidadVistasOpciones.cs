using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Blazor.Services;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Apoyo común de las vistas contables (Task 7.4): etiquetas de los enums del libro y las cuentas de Posteo del select (TODAS,
/// también las bloqueadas: un filtro de consulta debe poder ver la historia de cualquier cuenta), con la vigente aunque no esté.
/// </summary>
public static class ContabilidadVistasOpciones
{
    public static readonly IReadOnlyList<(int Valor, string Etiqueta)> TiposDocumento =
    [
        ((int)TipoDocumentoContable.Ninguno, "Sin documento"),
        ((int)TipoDocumentoContable.CostoInventario, "Costo de inventario"),
        ((int)TipoDocumentoContable.FacturaVenta, "Factura de venta"),
        ((int)TipoDocumentoContable.Cobro, "Cobro"),
    ];

    public static string TipoDocumento(TipoDocumentoContable tipo) =>
        TiposDocumento.FirstOrDefault(x => x.Valor == (int)tipo).Etiqueta ?? tipo.ToString();

    public static string TipoResultado(TipoResultadoCuenta tipo) => tipo switch
    {
        TipoResultadoCuenta.Balance => "Balance",
        TipoResultadoCuenta.Resultado => "Resultado",
        _ => tipo.ToString()
    };

    /// <summary>Cuentas de Posteo (las únicas con movimientos), todas las páginas, por número.</summary>
    public static async Task<OpcionesCargadas<CuentaContableResponse>> CuentasPosteoAsync(
        ICuentaContableApiClient client, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProductoOpciones.TiempoMaximo);
        try
        {
            var todas = new List<CuentaContableResponse>();
            var filtro = new CuentaContableSearchFilter(null, null, TipoCuentaContable.Posteo, null, null);
            for (var pagina = 1; ; pagina++)
            {
                var resultado = await client.ListAsync(filtro, new PageRequest(pagina, PageRequest.TamanoMaximo, "Numero", Descendente: false), cts.Token);
                todas.AddRange(resultado.Items.Where(c => c.TipoCuenta == TipoCuentaContable.Posteo));
                if (resultado.Items.Count == 0 || pagina >= resultado.TotalPaginas)
                {
                    break;
                }
            }

            return new(todas, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load cuentas contables options from API.");
            return new([], true);
        }
    }

    /// <summary>La cuenta elegida, aunque no esté entre las opciones (p. ej. si la lista no se pudo cargar).</summary>
    public static async Task<CuentaContableResponse?> CuentaVigenteAsync(
        ICuentaContableApiClient client, Guid? cuentaId, IReadOnlyList<CuentaContableResponse> opciones, ILogger logger)
    {
        if (cuentaId is not { } id)
        {
            return null;
        }

        var enLista = opciones.FirstOrDefault(c => c.Id == id);
        if (enLista is not null)
        {
            return enLista;
        }

        try
        {
            return await client.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load cuenta contable {Id}.", id);
            return null;
        }
    }
}
