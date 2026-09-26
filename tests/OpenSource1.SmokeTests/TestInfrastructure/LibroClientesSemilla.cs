using Dapper;
using Npgsql;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Siembra por SQL directo facturas posteadas y el libro de clientes (Task 6.3, para las consultas: el motor de posteo de la Task
/// 6.4 tiene sus propios tests). Solo <c>INSERT</c> (las tablas son append-only). Usa las semillas de grupos, cuentas y almacén.
/// </summary>
internal static class LibroClientesSemilla
{
    // Parámetros DateOnly en Dapper sin arrancar la API (la API lo registra en AddApplicationData; registrarlo otra vez es inocuo).
    static LibroClientesSemilla() => SqlMapper.AddTypeHandler(new DapperDateOnlyTypeHandler());

    public sealed record Linea(
        int NumeroLinea,
        TipoLineaFactura Tipo,
        decimal ImporteLinea,
        string? IdentificadorIva = "ITBIS18",
        decimal PorcentajeIva = 18m,
        Guid? ProductoId = null,
        Guid? CuentaContableId = null,
        string? Descripcion = null);

    public sealed record LineaIva(string IdentificadorIva, decimal PorcentajeIva, decimal BaseImponible, decimal ImporteIva);

    public static async Task<Guid> InsertarSocioAsync(NpgsqlConnection conexion, string nombre)
    {
        var id = Guid.NewGuid();
        await conexion.ExecuteAsync(
            """
            INSERT INTO "SociosNegocio" ("Id","Codigo","Tipo","NombreComercial","TipoDocumentoFiscal","LimiteCredito","Bloqueado",
                "CreatedAtUtc","CreatedBy","IsDeleted","GrupoNegocioId","GrupoIvaNegocioId","GrupoClienteContableId")
            VALUES (@Id, @Codigo, 1, @Nombre, 0, 0, 0, now(), 'test', false, @GrupoNegocio, @GrupoIva, @GrupoCliente)
            """,
            new
            {
                Id = id,
                Codigo = $"LC{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                Nombre = nombre,
                GrupoNegocio = GrupoContableIds.NegocioNacional,
                GrupoIva = GrupoContableIds.IvaNegocioItbis18,
                GrupoCliente = GrupoContableIds.ClienteContableGeneral,
            });
        return id;
    }

    public static Task<long> InsertarRegistroContableAsync(NpgsqlConnection conexion, string clave) =>
        conexion.ExecuteScalarAsync<long>(
            """
            INSERT INTO "RegistrosContables" ("NumeroRegistro","DesdeMovimiento","HastaMovimiento","FechaCreacion","CreadoPor",
                "TipoOrigen","ClaveOrigen")
            VALUES (@Numero, 0, 0, now(), 'test', 2, @Clave)
            RETURNING "Id"
            """,
            new { Numero = $"T{Guid.NewGuid():N}"[..20], Clave = clave });

    /// <summary>Cabecera + líneas + líneas de IVA. <c>ImporteSinIva</c>/<c>ImporteIva</c> = suma de las líneas de IVA.</summary>
    public static async Task InsertarFacturaAsync(
        NpgsqlConnection conexion,
        string numero,
        Guid socioId,
        DateOnly fechaRegistro,
        IReadOnlyList<Linea> lineas,
        IReadOnlyList<LineaIva> lineasIva,
        long? registroContableId,
        string? nombreFacturacion = null,
        Guid? facturarAId = null)
    {
        var sinIva = lineasIva.Sum(l => l.BaseImponible);
        var iva = lineasIva.Sum(l => l.ImporteIva);
        await conexion.ExecuteAsync(
            """
            INSERT INTO "FacturasVenta" ("Numero","NumeroBorrador","SocioNegocioId","SocioNegocioFacturarAId","NombreFacturacion",
                "RazonSocialFacturacion","TipoDocumentoFiscal","NumeroDocumentoFiscal","DireccionFacturacionLinea1",
                "DireccionFacturacionLinea2","CiudadFacturacion","PaisCodigoFacturacion","FechaRegistro","FechaDocumento",
                "FechaVencimiento","TerminoPagoId","GrupoNegocioId","GrupoIvaNegocioId","GrupoClienteContableId","AlmacenId","Moneda",
                "Descripcion","ImporteSinIva","ImporteIva","ImporteTotal","RegistroContableId","CreatedAtUtc","CreatedBy","UsuarioId")
            VALUES (@Numero, @NumeroBorrador, @SocioId, @FacturarAId, @Nombre, 'Razón SRL', 1, '101000001', 'Calle 1', NULL, 'Santiago',
                'DO', @Fecha, @Fecha, @Vence, NULL, @GrupoNegocio, @GrupoIva, @GrupoCliente, @Almacen, 'DOP', 'Factura de prueba',
                @SinIva, @Iva, @Total, @RegistroId, now(), 'test', NULL)
            """,
            new
            {
                Numero = numero,
                NumeroBorrador = $"B{numero}",
                SocioId = socioId,
                FacturarAId = facturarAId ?? socioId,
                Nombre = nombreFacturacion ?? "Cliente posteado",
                Fecha = fechaRegistro,
                Vence = fechaRegistro.AddDays(30),
                GrupoNegocio = GrupoContableIds.NegocioNacional,
                GrupoIva = GrupoContableIds.IvaNegocioItbis18,
                GrupoCliente = GrupoContableIds.ClienteContableGeneral,
                Almacen = AlmacenIds.Principal,
                SinIva = sinIva,
                Iva = iva,
                Total = sinIva + iva,
                RegistroId = registroContableId,
            });

        foreach (var linea in lineas)
        {
            var comentario = linea.Tipo == TipoLineaFactura.Comentario;

            // CHECK de la Task 6.4: una línea de Producto posteada lleva su salida de inventario. La semilla la inserta directa
            // (sin valor ni aplicaciones), como la apertura legada de LibroInventarioPrueba.
            long? movimientoProductoId = linea.Tipo == TipoLineaFactura.Producto
                ? await conexion.ExecuteScalarAsync<long>(
                    """
                    INSERT INTO "MovimientosProducto" (
                        "ProductoId", "AlmacenId", "TipoMovimiento", "TipoDocumento", "NumeroDocumento", "NumeroLineaDocumento",
                        "FechaRegistro", "FechaDocumento", "Cantidad", "CantidadRestante", "CantidadFacturada", "UnidadMedidaId",
                        "CantidadPorUnidadMedida", "TipoOrigen", "ClaveOrigen", "CreatedAtUtc", "CreatedBy")
                    SELECT @ProductoId, @AlmacenId, 2, 2, @Numero, @NumeroLinea, @Fecha, @Fecha, -1, NULL, 0, p."UnidadMedidaBaseId",
                        1, 2, @Numero, now(), 'test'
                    FROM "Productos" p WHERE p."Id" = @ProductoId
                    RETURNING "Id"
                    """,
                    new { linea.ProductoId, AlmacenId = AlmacenIds.Principal, Numero = numero, linea.NumeroLinea, Fecha = fechaRegistro })
                : null;
            await conexion.ExecuteAsync(
                """
                INSERT INTO "LineasFacturaVenta" ("FacturaVentaNumero","NumeroLinea","Tipo","ProductoId","CuentaContableId","Descripcion",
                    "AlmacenId","UnidadMedidaId","CantidadPorUnidadMedida","Cantidad","PrecioUnitario","PorcentajeDescuentoLinea",
                    "ImporteDescuentoLinea","ImporteLinea","GrupoProductoId","GrupoIvaProductoId","GrupoInventarioId","IdentificadorIva",
                    "PorcentajeIva","MovimientoProductoId")
                VALUES (@Numero, @NumeroLinea, @Tipo, @ProductoId, @CuentaId, @Descripcion, @AlmacenId, NULL, @Factor, @Cantidad, @Precio,
                    0, 0, @Importe, NULL, @GrupoIvaProducto, NULL, @Identificador, @Porcentaje, @MovimientoProductoId)
                """,
                new
                {
                    Numero = numero,
                    linea.NumeroLinea,
                    Tipo = (short)linea.Tipo,
                    linea.ProductoId,
                    CuentaId = linea.CuentaContableId,
                    linea.Descripcion,
                    AlmacenId = linea.Tipo == TipoLineaFactura.Producto ? AlmacenIds.Principal : (Guid?)null,
                    Factor = comentario ? 0m : 1m,
                    Cantidad = comentario ? 0m : 1m,
                    Precio = linea.ImporteLinea,
                    Importe = linea.ImporteLinea,
                    GrupoIvaProducto = comentario ? (Guid?)null : GrupoContableIds.IvaProductoItbis18,
                    Identificador = comentario ? null : linea.IdentificadorIva,
                    Porcentaje = comentario ? 0m : linea.PorcentajeIva,
                    MovimientoProductoId = movimientoProductoId,
                });
        }

        foreach (var linea in lineasIva)
        {
            await conexion.ExecuteAsync(
                """
                INSERT INTO "LineasIvaFacturaVenta" ("FacturaVentaNumero","IdentificadorIva","PorcentajeIva","BaseImponible","ImporteIva","CuentaIvaId")
                VALUES (@Numero, @IdentificadorIva, @PorcentajeIva, @BaseImponible, @ImporteIva, @Cuenta)
                """,
                new { Numero = numero, linea.IdentificadorIva, linea.PorcentajeIva, linea.BaseImponible, linea.ImporteIva, Cuenta = CuentaContableIds.IvaPorPagar });
        }
    }

    /// <summary>Movimiento del libro de clientes y su detalle <c>ImporteInicial</c> (o <c>Pago</c> si el tipo es Pago).</summary>
    public static async Task<long> InsertarMovimientoAsync(
        NpgsqlConnection conexion,
        Guid socioId,
        TipoDocumentoCliente tipo,
        string numeroDocumento,
        DateOnly fechaRegistro,
        decimal importe,
        TipoOrigenMovimiento origen = TipoOrigenMovimiento.FacturaVenta)
    {
        var id = await conexion.ExecuteScalarAsync<long>(
            """
            INSERT INTO "MovimientosCliente" ("SocioNegocioId","FechaRegistro","FechaDocumento","FechaVencimiento","TipoDocumento",
                "NumeroDocumento","Descripcion","ImporteOriginal","GrupoClienteContableId","CuentaCxCId","TipoOrigen","ClaveOrigen",
                "CreatedAtUtc","CreatedBy")
            VALUES (@SocioId, @Fecha, @Fecha, @Vence, @Tipo, @Numero, @Descripcion, @Importe, @GrupoCliente, @CuentaCxC, @Origen, @Numero,
                now(), 'test')
            RETURNING "Id"
            """,
            new
            {
                SocioId = socioId,
                Fecha = fechaRegistro,
                Vence = fechaRegistro.AddDays(30),
                Tipo = (short)tipo,
                Numero = numeroDocumento,
                Descripcion = $"{tipo} {numeroDocumento}",
                Importe = importe,
                GrupoCliente = GrupoContableIds.ClienteContableGeneral,
                CuentaCxC = CuentaContableIds.CxC,
                Origen = (short)origen,
            });

        await InsertarDetalleAsync(
            conexion, id, TipoDetalleCliente.ImporteInicial, importe, fechaRegistro, null, origen, numeroDocumento);
        return id;
    }

    public static Task InsertarDetalleAsync(
        NpgsqlConnection conexion,
        long movimientoId,
        TipoDetalleCliente tipo,
        decimal importe,
        DateOnly fechaRegistro,
        long? aplicadoId,
        TipoOrigenMovimiento origen = TipoOrigenMovimiento.Cobro,
        string clave = "APLIC") =>
        conexion.ExecuteAsync(
            """
            INSERT INTO "MovimientosClienteDetalle" ("MovimientoClienteId","TipoMovimiento","Importe","FechaRegistro",
                "MovimientoClienteAplicadoId","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
            VALUES (@MovimientoId, @Tipo, @Importe, @Fecha, @AplicadoId, @Origen, @Clave, now(), 'test')
            """,
            new
            {
                MovimientoId = movimientoId,
                Tipo = (short)tipo,
                Importe = importe,
                Fecha = fechaRegistro,
                AplicadoId = aplicadoId,
                Origen = (short)origen,
                Clave = clave,
            });

    /// <summary>Aplicación: dos filas de detalle de signo opuesto que se apuntan mutuamente (spec 6.4, D3).</summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, long facturaId, long pagoId, decimal importe, DateOnly fecha)
    {
        await InsertarDetalleAsync(conexion, facturaId, TipoDetalleCliente.Aplicacion, -importe, fecha, pagoId);
        await InsertarDetalleAsync(conexion, pagoId, TipoDetalleCliente.Aplicacion, importe, fecha, facturaId);
    }
}
