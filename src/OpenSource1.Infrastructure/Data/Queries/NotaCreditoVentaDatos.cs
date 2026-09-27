using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.NotasCreditoVenta;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Acceso a datos de las notas de crédito de venta (Task 8.6) con Dapper sobre la transacción de <see cref="IDbSession"/>. Las tres
/// tablas del documento posteado son append-only: aquí solo hay <c>INSERT</c> sobre ellas.
/// </summary>
public sealed class NotaCreditoVentaDatos(IDbSession session) : INotaCreditoVentaDatos
{
    public async Task<bool> BloquearBorradorAsync(Guid notaCreditoVentaBorradorId, CancellationToken cancellationToken = default)
    {
        await AsegurarTransaccionAsync(cancellationToken);
        var id = await session.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """SELECT "Id" FROM "NotasCreditoVentaBorrador" WHERE "Id" = @Id AND "IsDeleted" = false FOR UPDATE""",
            new { Id = notaCreditoVentaBorradorId }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return id is not null;
    }

    public async Task<IReadOnlyList<LineaNotaAPostear>> BloquearLineasAsync(Guid notaCreditoVentaBorradorId, CancellationToken cancellationToken = default)
    {
        await AsegurarTransaccionAsync(cancellationToken);
        var filas = await session.Connection.QueryAsync<LineaFila>(new CommandDefinition(
            """
            SELECT "Id", "LineaFacturaVentaId", "NumeroLinea", "Tipo", "ProductoId", "CuentaContableId", "Descripcion", "AlmacenId",
                   "UnidadMedidaId", "CantidadPorUnidadMedida", "Cantidad", "PrecioUnitario", "PorcentajeDescuentoLinea",
                   "ImporteDescuentoLinea", "ImporteLinea", "GrupoProductoId", "GrupoIvaProductoId", "GrupoInventarioId",
                   "IdentificadorIva", "PorcentajeIva", "DevolverInventario"
            FROM "LineasNotaCreditoVentaBorrador"
            WHERE "NotaCreditoVentaBorradorId" = @Id AND "IsDeleted" = false
            ORDER BY "NumeroLinea"
            FOR UPDATE
            """,
            new { Id = notaCreditoVentaBorradorId }, session.CurrentTransaction, cancellationToken: cancellationToken));

        return [.. filas.Select(f => new LineaNotaAPostear(
            f.Id, f.LineaFacturaVentaId, f.NumeroLinea, (TipoLineaFactura)f.Tipo, f.ProductoId, f.CuentaContableId, f.Descripcion,
            f.AlmacenId, f.UnidadMedidaId, f.CantidadPorUnidadMedida, f.Cantidad, f.PrecioUnitario, f.PorcentajeDescuentoLinea,
            f.ImporteDescuentoLinea, f.ImporteLinea, f.GrupoProductoId, f.GrupoIvaProductoId, f.GrupoInventarioId, f.IdentificadorIva,
            f.PorcentajeIva, f.DevolverInventario))];
    }

    public async Task<int> BorrarLineasAsync(Guid notaCreditoVentaBorradorId, string usuario, CancellationToken cancellationToken = default)
    {
        await AsegurarTransaccionAsync(cancellationToken);
        return await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE "LineasNotaCreditoVentaBorrador" SET "IsDeleted" = true, "DeletedAtUtc" = @Ahora, "DeletedBy" = @Usuario
            WHERE "NotaCreditoVentaBorradorId" = @Id AND "IsDeleted" = false
            """,
            new { Id = notaCreditoVentaBorradorId, Ahora = DateTimeOffset.UtcNow, Usuario = usuario },
            session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task BloquearSociosAsync(IEnumerable<Guid> socioNegocioIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socioNegocioIds);
        await AsegurarTransaccionAsync(cancellationToken);
        await session.Connection.ExecuteAsync(new CommandDefinition(
            """SELECT 1 FROM "SociosNegocio" WHERE "Id" = ANY(@Ids) ORDER BY "Id" FOR SHARE""",
            new { Ids = socioNegocioIds.Distinct().Order().ToArray() }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<FacturaVenta?> ObtenerFacturaAsync(string numero, bool bloquear, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(numero);
        if (bloquear)
        {
            await AsegurarTransaccionAsync(cancellationToken);
        }
        else
        {
            await session.EnsureOpenAsync(cancellationToken);
        }

        var sql = bloquear
            ? """SELECT * FROM "FacturasVenta" WHERE "Numero" = @Numero FOR UPDATE"""
            : """SELECT * FROM "FacturasVenta" WHERE "Numero" = @Numero""";
        return await session.Connection.QuerySingleOrDefaultAsync<FacturaVenta>(new CommandDefinition(
            sql, new { Numero = numero }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LineaFacturaVenta>> LineasFacturaAsync(string facturaNumero, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        var filas = await session.Connection.QueryAsync<LineaFacturaVenta>(new CommandDefinition(
            """SELECT * FROM "LineasFacturaVenta" WHERE "FacturaVentaNumero" = @Numero ORDER BY "NumeroLinea" """,
            new { Numero = facturaNumero }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return filas.AsList();
    }

    public async Task<IReadOnlyDictionary<long, decimal>> CantidadesAcreditadasAsync(string facturaNumero, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        var filas = await session.Connection.QueryAsync<(long LineaId, decimal Cantidad)>(new CommandDefinition(
            """
            SELECT n."LineaFacturaVentaId", SUM(n."Cantidad")
            FROM "LineasNotaCreditoVenta" n
            JOIN "LineasFacturaVenta" f ON f."Id" = n."LineaFacturaVentaId"
            WHERE f."FacturaVentaNumero" = @Numero
            GROUP BY n."LineaFacturaVentaId"
            """,
            new { Numero = facturaNumero }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return filas.ToDictionary(f => f.LineaId, f => f.Cantidad);
    }

    public async Task<MovimientoClienteFactura?> MovimientoClienteFacturaAsync(string facturaNumero, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<MovimientoClienteFactura>(new CommandDefinition(
            """
            SELECT "Id", "SocioNegocioId", "GrupoClienteContableId", "CuentaCxCId" FROM "MovimientosCliente"
            WHERE "TipoDocumento" = @Tipo AND "NumeroDocumento" = @Numero
            """,
            new { Tipo = (short)TipoDocumentoCliente.Factura, Numero = facturaNumero }, session.CurrentTransaction,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<string, Guid>> CuentasIvaFacturaAsync(string facturaNumero, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        var filas = await session.Connection.QueryAsync<(string Identificador, Guid Cuenta)>(new CommandDefinition(
            """SELECT "IdentificadorIva", "CuentaIvaId" FROM "LineasIvaFacturaVenta" WHERE "FacturaVentaNumero" = @Numero""",
            new { Numero = facturaNumero }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return filas.ToDictionary(f => f.Identificador, f => f.Cuenta, StringComparer.Ordinal);
    }

    public async Task<CostoSalidaVenta?> CostoSalidaAsync(long movimientoProductoId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        // CostoDirecto = original + ajustes de costo (misma suma que "actual" en la rutina de ajuste); sin los de Redondeo.
        return await session.Connection.QuerySingleOrDefaultAsync<CostoSalidaVenta>(new CommandDefinition(
            """
            SELECT ABS(m."Cantidad") AS "CantidadBase",
                   COALESCE((SELECT SUM(v."ImporteCosto") FROM "MovimientosValor" v
                             WHERE v."MovimientoProductoId" = m."Id" AND v."TipoValor" = @CostoDirecto), 0) AS "ImporteCosto"
            FROM "MovimientosProducto" m
            WHERE m."Id" = @Id
            """,
            new { Id = movimientoProductoId, CostoDirecto = (short)TipoValor.CostoDirecto }, session.CurrentTransaction,
            cancellationToken: cancellationToken));
    }

    public async Task InsertarNotaAsync(
        NotaCreditoVenta nota,
        IReadOnlyList<LineaNotaCreditoVenta> lineas,
        IReadOnlyList<LineaIvaNotaCreditoVenta> lineasIva,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nota);
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(lineasIva);
        await AsegurarTransaccionAsync(cancellationToken);
        var tx = session.CurrentTransaction;

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "NotasCreditoVenta" (
                "Numero", "NumeroBorrador", "FacturaVentaNumero", "SocioNegocioId", "SocioNegocioFacturarAId", "NombreFacturacion",
                "RazonSocialFacturacion", "TipoDocumentoFiscal", "NumeroDocumentoFiscal", "DireccionFacturacionLinea1",
                "DireccionFacturacionLinea2", "CiudadFacturacion", "PaisCodigoFacturacion", "FechaRegistro", "FechaDocumento",
                "GrupoNegocioId", "GrupoIvaNegocioId", "GrupoClienteContableId", "CuentaCxCId", "Moneda", "Descripcion",
                "ImporteSinIva", "ImporteIva", "ImporteTotal", "RegistroContableId", "CreatedAtUtc", "CreatedBy", "UsuarioId")
            VALUES (
                @Numero, @NumeroBorrador, @FacturaVentaNumero, @SocioNegocioId, @SocioNegocioFacturarAId, @NombreFacturacion,
                @RazonSocialFacturacion, @TipoDocumentoFiscal, @NumeroDocumentoFiscal, @DireccionFacturacionLinea1,
                @DireccionFacturacionLinea2, @CiudadFacturacion, @PaisCodigoFacturacion, @FechaRegistro, @FechaDocumento,
                @GrupoNegocioId, @GrupoIvaNegocioId, @GrupoClienteContableId, @CuentaCxCId, @Moneda, @Descripcion,
                @ImporteSinIva, @ImporteIva, @ImporteTotal, @RegistroContableId, @CreatedAtUtc, @CreatedBy, @UsuarioId)
            """,
            nota, tx, cancellationToken: cancellationToken));

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "LineasNotaCreditoVenta" (
                "NotaCreditoVentaNumero", "NumeroLinea", "LineaFacturaVentaId", "Tipo", "ProductoId", "CuentaContableId", "Descripcion",
                "AlmacenId", "UnidadMedidaId", "CantidadPorUnidadMedida", "Cantidad", "PrecioUnitario", "PorcentajeDescuentoLinea",
                "ImporteDescuentoLinea", "ImporteLinea", "GrupoProductoId", "GrupoIvaProductoId", "GrupoInventarioId",
                "IdentificadorIva", "PorcentajeIva", "DevolverInventario", "MovimientoProductoId")
            VALUES (
                @NotaCreditoVentaNumero, @NumeroLinea, @LineaFacturaVentaId, @Tipo, @ProductoId, @CuentaContableId, @Descripcion,
                @AlmacenId, @UnidadMedidaId, @CantidadPorUnidadMedida, @Cantidad, @PrecioUnitario, @PorcentajeDescuentoLinea,
                @ImporteDescuentoLinea, @ImporteLinea, @GrupoProductoId, @GrupoIvaProductoId, @GrupoInventarioId,
                @IdentificadorIva, @PorcentajeIva, @DevolverInventario, @MovimientoProductoId)
            """,
            lineas, tx, cancellationToken: cancellationToken));

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "LineasIvaNotaCreditoVenta" (
                "NotaCreditoVentaNumero", "IdentificadorIva", "PorcentajeIva", "BaseImponible", "ImporteIva", "CuentaIvaId")
            VALUES (@NotaCreditoVentaNumero, @IdentificadorIva, @PorcentajeIva, @BaseImponible, @ImporteIva, @CuentaIvaId)
            """,
            lineasIva, tx, cancellationToken: cancellationToken));
    }

    private async Task AsegurarTransaccionAsync(CancellationToken cancellationToken)
    {
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Esta operación de notas de crédito de venta requiere una transacción activa.");
        }

        await session.EnsureOpenAsync(cancellationToken);
    }

    private sealed class LineaFila
    {
        public Guid Id { get; init; }
        public long LineaFacturaVentaId { get; init; }
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
        public bool DevolverInventario { get; init; }
    }
}
