using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.FacturasVenta.Posteo;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Acceso a datos del posteo de facturas de venta (Task 6.4) con Dapper sobre la transacción de <see cref="IDbSession"/>. Las
/// tres tablas del documento posteado son append-only: aquí solo hay <c>INSERT</c>.
/// </summary>
public sealed class PosteoFacturaVentaDatos(IDbSession session) : IPosteoFacturaVentaDatos
{
    public async Task<IReadOnlyList<LineaFacturaAPostear>> BloquearLineasAsync(Guid facturaVentaBorradorId, CancellationToken cancellationToken = default)
    {
        await AsegurarTransaccionAsync(cancellationToken);

        const string sql = """
            SELECT "Id", "NumeroLinea", "Tipo", "ProductoId", "CuentaContableId", "Descripcion", "AlmacenId", "UnidadMedidaId",
                   "CantidadPorUnidadMedida", "Cantidad", "PrecioUnitario", "PorcentajeDescuentoLinea", "ImporteDescuentoLinea",
                   "ImporteLinea", "GrupoProductoId", "GrupoIvaProductoId", "GrupoInventarioId", "IdentificadorIva", "PorcentajeIva"
            FROM "LineasFacturaVentaBorrador"
            WHERE "FacturaVentaBorradorId" = @Id AND "IsDeleted" = false
            ORDER BY "NumeroLinea"
            FOR UPDATE
            """;

        var filas = await session.Connection.QueryAsync<LineaFila>(new CommandDefinition(
            sql, new { Id = facturaVentaBorradorId }, session.CurrentTransaction, cancellationToken: cancellationToken));

        return [.. filas.Select(f => new LineaFacturaAPostear(
            f.Id, f.NumeroLinea, (TipoLineaFactura)f.Tipo, f.ProductoId, f.CuentaContableId, f.Descripcion, f.AlmacenId,
            f.UnidadMedidaId, f.CantidadPorUnidadMedida, f.Cantidad, f.PrecioUnitario, f.PorcentajeDescuentoLinea,
            f.ImporteDescuentoLinea, f.ImporteLinea, f.GrupoProductoId, f.GrupoIvaProductoId, f.GrupoInventarioId,
            f.IdentificadorIva, f.PorcentajeIva))];
    }

    public async Task BloquearSociosAsync(IEnumerable<Guid> socioNegocioIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socioNegocioIds);
        await AsegurarTransaccionAsync(cancellationToken);

        // Orden único por Id (sin repetir): dos posteos con los mismos socios en distinto papel no se interbloquean.
        await session.Connection.ExecuteAsync(new CommandDefinition(
            """SELECT 1 FROM "SociosNegocio" WHERE "Id" = ANY(@Ids) ORDER BY "Id" FOR SHARE""",
            new { Ids = socioNegocioIds.Distinct().Order().ToArray() }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task InsertarFacturaAsync(
        FacturaVenta factura,
        IReadOnlyList<LineaFacturaVenta> lineas,
        IReadOnlyList<LineaIvaFacturaVenta> lineasIva,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factura);
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(lineasIva);
        await AsegurarTransaccionAsync(cancellationToken);
        var tx = session.CurrentTransaction;

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "FacturasVenta" (
                "Numero", "NumeroBorrador", "SocioNegocioId", "SocioNegocioFacturarAId", "NombreFacturacion", "RazonSocialFacturacion",
                "TipoDocumentoFiscal", "NumeroDocumentoFiscal", "DireccionFacturacionLinea1", "DireccionFacturacionLinea2",
                "CiudadFacturacion", "PaisCodigoFacturacion", "FechaRegistro", "FechaDocumento", "FechaVencimiento", "TerminoPagoId",
                "GrupoNegocioId", "GrupoIvaNegocioId", "GrupoClienteContableId", "AlmacenId", "Moneda", "Descripcion",
                "ImporteSinIva", "ImporteIva", "ImporteTotal", "RegistroContableId", "CreatedAtUtc", "CreatedBy", "UsuarioId")
            VALUES (
                @Numero, @NumeroBorrador, @SocioNegocioId, @SocioNegocioFacturarAId, @NombreFacturacion, @RazonSocialFacturacion,
                @TipoDocumentoFiscal, @NumeroDocumentoFiscal, @DireccionFacturacionLinea1, @DireccionFacturacionLinea2,
                @CiudadFacturacion, @PaisCodigoFacturacion, @FechaRegistro, @FechaDocumento, @FechaVencimiento, @TerminoPagoId,
                @GrupoNegocioId, @GrupoIvaNegocioId, @GrupoClienteContableId, @AlmacenId, @Moneda, @Descripcion,
                @ImporteSinIva, @ImporteIva, @ImporteTotal, @RegistroContableId, @CreatedAtUtc, @CreatedBy, @UsuarioId)
            """,
            factura, tx, cancellationToken: cancellationToken));

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "LineasFacturaVenta" (
                "FacturaVentaNumero", "NumeroLinea", "Tipo", "ProductoId", "CuentaContableId", "Descripcion", "AlmacenId",
                "UnidadMedidaId", "CantidadPorUnidadMedida", "Cantidad", "PrecioUnitario", "PorcentajeDescuentoLinea",
                "ImporteDescuentoLinea", "ImporteLinea", "GrupoProductoId", "GrupoIvaProductoId", "GrupoInventarioId",
                "IdentificadorIva", "PorcentajeIva", "MovimientoProductoId")
            VALUES (
                @FacturaVentaNumero, @NumeroLinea, @Tipo, @ProductoId, @CuentaContableId, @Descripcion, @AlmacenId,
                @UnidadMedidaId, @CantidadPorUnidadMedida, @Cantidad, @PrecioUnitario, @PorcentajeDescuentoLinea,
                @ImporteDescuentoLinea, @ImporteLinea, @GrupoProductoId, @GrupoIvaProductoId, @GrupoInventarioId,
                @IdentificadorIva, @PorcentajeIva, @MovimientoProductoId)
            """,
            lineas, tx, cancellationToken: cancellationToken));

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "LineasIvaFacturaVenta" (
                "FacturaVentaNumero", "IdentificadorIva", "PorcentajeIva", "BaseImponible", "ImporteIva", "CuentaIvaId")
            VALUES (@FacturaVentaNumero, @IdentificadorIva, @PorcentajeIva, @BaseImponible, @ImporteIva, @CuentaIvaId)
            """,
            lineasIva, tx, cancellationToken: cancellationToken));
    }

    private async Task AsegurarTransaccionAsync(CancellationToken cancellationToken)
    {
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("El posteo de una factura de venta requiere una transacción activa.");
        }

        await session.EnsureOpenAsync(cancellationToken);
    }

    private sealed class LineaFila
    {
        public Guid Id { get; init; }
        public int NumeroLinea { get; init; }
        public short Tipo { get; init; }
        public Guid? ProductoId { get; init; }
        public Guid? CuentaContableId { get; init; }
        public string? Descripcion { get; init; }
        public Guid? AlmacenId { get; init; }
        public Guid? UnidadMedidaId { get; init; }
        public decimal CantidadPorUnidadMedida { get; init; }
        public decimal Cantidad { get; init; }
        public decimal PrecioUnitario { get; init; }
        public decimal PorcentajeDescuentoLinea { get; init; }
        public decimal ImporteDescuentoLinea { get; init; }
        public decimal ImporteLinea { get; init; }
        public Guid? GrupoProductoId { get; init; }
        public Guid? GrupoIvaProductoId { get; init; }
        public Guid? GrupoInventarioId { get; init; }
        public string? IdentificadorIva { get; init; }
        public decimal PorcentajeIva { get; init; }
    }
}
