using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <summary>
    /// Task 6.3: factura de venta posteada (<c>FacturasVenta</c> con PK <c>Numero</c>, <c>LineasFacturaVenta</c>,
    /// <c>LineasIvaFacturaVenta</c>) y libro de clientes con detalle (<c>MovimientosCliente</c>, <c>MovimientosClienteDetalle</c>),
    /// las cinco tablas append-only. Sin datos: las escriben el motor de posteo (Task 6.4) y los cobros (Task 6.5).
    /// </summary>
    public partial class AddFacturasVentaYLibroClientes : Migration
    {
        /// <summary>Las cinco tablas del documento posteado y del libro de clientes, todas append-only.</summary>
        private static readonly string[] TablasAppendOnly =
            ["FacturasVenta", "LineasFacturaVenta", "LineasIvaFacturaVenta", "MovimientosCliente", "MovimientosClienteDetalle"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacturasVenta",
                columns: table => new
                {
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroBorrador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SocioNegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocioNegocioFacturarAId = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreFacturacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RazonSocialFacturacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TipoDocumentoFiscal = table.Column<short>(type: "smallint", nullable: false),
                    NumeroDocumentoFiscal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DireccionFacturacionLinea1 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DireccionFacturacionLinea2 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CiudadFacturacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaisCodigoFacturacion = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FechaRegistro = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    TerminoPagoId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoNegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoIvaNegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoClienteContableId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImporteSinIva = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImporteIva = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImporteTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RegistroContableId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasVenta", x => x.Numero);
                    table.CheckConstraint("CK_FacturasVenta_Total", "\"ImporteTotal\" = \"ImporteSinIva\" + \"ImporteIva\"");
                    table.ForeignKey(
                        name: "FK_FacturasVenta_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVenta_GruposClienteContable_GrupoClienteContableId",
                        column: x => x.GrupoClienteContableId,
                        principalTable: "GruposClienteContable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVenta_GruposIvaNegocio_GrupoIvaNegocioId",
                        column: x => x.GrupoIvaNegocioId,
                        principalTable: "GruposIvaNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVenta_GruposNegocio_GrupoNegocioId",
                        column: x => x.GrupoNegocioId,
                        principalTable: "GruposNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVenta_RegistrosContables_RegistroContableId",
                        column: x => x.RegistroContableId,
                        principalTable: "RegistrosContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVenta_SociosNegocio_SocioNegocioFacturarAId",
                        column: x => x.SocioNegocioFacturarAId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVenta_SociosNegocio_SocioNegocioId",
                        column: x => x.SocioNegocioId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVenta_TerminosPago_TerminoPagoId",
                        column: x => x.TerminoPagoId,
                        principalTable: "TerminosPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCliente",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    SocioNegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaRegistro = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoDocumento = table.Column<short>(type: "smallint", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImporteOriginal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrupoClienteContableId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaCxCId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoOrigen = table.Column<short>(type: "smallint", nullable: false),
                    ClaveOrigen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCliente", x => x.Id);
                    table.CheckConstraint("CK_MovimientosCliente_TipoDocumento", "\"TipoDocumento\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_MovimientosCliente_CuentasContables_CuentaCxCId",
                        column: x => x.CuentaCxCId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosCliente_GruposClienteContable_GrupoClienteContab~",
                        column: x => x.GrupoClienteContableId,
                        principalTable: "GruposClienteContable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosCliente_SociosNegocio_SocioNegocioId",
                        column: x => x.SocioNegocioId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasFacturaVenta",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    FacturaVentaNumero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroLinea = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<short>(type: "smallint", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CuentaContableId = table.Column<Guid>(type: "uuid", nullable: true),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AlmacenId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnidadMedidaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CantidadPorUnidadMedida = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PorcentajeDescuentoLinea = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
                    ImporteDescuentoLinea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImporteLinea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrupoProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoIvaProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoInventarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentificadorIva = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PorcentajeIva = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
                    MovimientoProductoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasFacturaVenta", x => x.Id);
                    table.CheckConstraint("CK_LineasFacturaVenta_Cantidad", "(\"Tipo\" = 3 AND \"Cantidad\" = 0) OR (\"Tipo\" <> 3 AND \"Cantidad\" > 0)");
                    table.CheckConstraint("CK_LineasFacturaVenta_Referencia", "(\"Tipo\" = 1 AND \"ProductoId\" IS NOT NULL AND \"CuentaContableId\" IS NULL) OR (\"Tipo\" = 2 AND \"CuentaContableId\" IS NOT NULL AND \"ProductoId\" IS NULL AND \"MovimientoProductoId\" IS NULL) OR (\"Tipo\" = 3 AND \"ProductoId\" IS NULL AND \"CuentaContableId\" IS NULL AND \"MovimientoProductoId\" IS NULL)");
                    table.CheckConstraint("CK_LineasFacturaVenta_Tipo", "\"Tipo\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_CuentasContables_CuentaContableId",
                        column: x => x.CuentaContableId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_FacturasVenta_FacturaVentaNumero",
                        column: x => x.FacturaVentaNumero,
                        principalTable: "FacturasVenta",
                        principalColumn: "Numero",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_GruposInventario_GrupoInventarioId",
                        column: x => x.GrupoInventarioId,
                        principalTable: "GruposInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_GruposIvaProducto_GrupoIvaProductoId",
                        column: x => x.GrupoIvaProductoId,
                        principalTable: "GruposIvaProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_GruposProducto_GrupoProductoId",
                        column: x => x.GrupoProductoId,
                        principalTable: "GruposProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_MovimientosProducto_MovimientoProductoId",
                        column: x => x.MovimientoProductoId,
                        principalTable: "MovimientosProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVenta_UnidadesMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalTable: "UnidadesMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasIvaFacturaVenta",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    FacturaVentaNumero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdentificadorIva = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PorcentajeIva = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
                    BaseImponible = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImporteIva = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CuentaIvaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasIvaFacturaVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasIvaFacturaVenta_CuentasContables_CuentaIvaId",
                        column: x => x.CuentaIvaId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasIvaFacturaVenta_FacturasVenta_FacturaVentaNumero",
                        column: x => x.FacturaVentaNumero,
                        principalTable: "FacturasVenta",
                        principalColumn: "Numero",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosClienteDetalle",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    MovimientoClienteId = table.Column<long>(type: "bigint", nullable: false),
                    TipoMovimiento = table.Column<short>(type: "smallint", nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FechaRegistro = table.Column<DateOnly>(type: "date", nullable: false),
                    MovimientoClienteAplicadoId = table.Column<long>(type: "bigint", nullable: true),
                    TipoOrigen = table.Column<short>(type: "smallint", nullable: false),
                    ClaveOrigen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosClienteDetalle", x => x.Id);
                    table.CheckConstraint("CK_MovimientosClienteDetalle_Aplicado", "(\"TipoMovimiento\" <> 3 OR \"MovimientoClienteAplicadoId\" IS NOT NULL) AND (\"MovimientoClienteAplicadoId\" IS NULL OR \"MovimientoClienteAplicadoId\" <> \"MovimientoClienteId\")");
                    table.CheckConstraint("CK_MovimientosClienteDetalle_TipoMovimiento", "\"TipoMovimiento\" IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_MovimientosClienteDetalle_MovimientosCliente_MovimientoClie~",
                        column: x => x.MovimientoClienteAplicadoId,
                        principalTable: "MovimientosCliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosClienteDetalle_MovimientosCliente_MovimientoCli~1",
                        column: x => x.MovimientoClienteId,
                        principalTable: "MovimientosCliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_AlmacenId",
                table: "FacturasVenta",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_FechaRegistro",
                table: "FacturasVenta",
                column: "FechaRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_GrupoClienteContableId",
                table: "FacturasVenta",
                column: "GrupoClienteContableId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_GrupoIvaNegocioId",
                table: "FacturasVenta",
                column: "GrupoIvaNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_GrupoNegocioId",
                table: "FacturasVenta",
                column: "GrupoNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_NumeroBorrador",
                table: "FacturasVenta",
                column: "NumeroBorrador",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_RegistroContableId",
                table: "FacturasVenta",
                column: "RegistroContableId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_SocioNegocioFacturarAId_FechaRegistro",
                table: "FacturasVenta",
                columns: new[] { "SocioNegocioFacturarAId", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_SocioNegocioId_FechaRegistro",
                table: "FacturasVenta",
                columns: new[] { "SocioNegocioId", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_TerminoPagoId",
                table: "FacturasVenta",
                column: "TerminoPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_AlmacenId",
                table: "LineasFacturaVenta",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_CuentaContableId",
                table: "LineasFacturaVenta",
                column: "CuentaContableId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_FacturaVentaNumero_NumeroLinea",
                table: "LineasFacturaVenta",
                columns: new[] { "FacturaVentaNumero", "NumeroLinea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_GrupoInventarioId",
                table: "LineasFacturaVenta",
                column: "GrupoInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_GrupoIvaProductoId",
                table: "LineasFacturaVenta",
                column: "GrupoIvaProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_GrupoProductoId",
                table: "LineasFacturaVenta",
                column: "GrupoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_MovimientoProductoId",
                table: "LineasFacturaVenta",
                column: "MovimientoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_ProductoId",
                table: "LineasFacturaVenta",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVenta_UnidadMedidaId",
                table: "LineasFacturaVenta",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasIvaFacturaVenta_CuentaIvaId",
                table: "LineasIvaFacturaVenta",
                column: "CuentaIvaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasIvaFacturaVenta_FacturaVentaNumero_IdentificadorIva",
                table: "LineasIvaFacturaVenta",
                columns: new[] { "FacturaVentaNumero", "IdentificadorIva" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCliente_CuentaCxCId",
                table: "MovimientosCliente",
                column: "CuentaCxCId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCliente_GrupoClienteContableId",
                table: "MovimientosCliente",
                column: "GrupoClienteContableId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCliente_SocioNegocioId_FechaRegistro",
                table: "MovimientosCliente",
                columns: new[] { "SocioNegocioId", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCliente_TipoDocumento_NumeroDocumento",
                table: "MovimientosCliente",
                columns: new[] { "TipoDocumento", "NumeroDocumento" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCliente_TipoOrigen_ClaveOrigen",
                table: "MovimientosCliente",
                columns: new[] { "TipoOrigen", "ClaveOrigen" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosClienteDetalle_MovimientoClienteAplicadoId",
                table: "MovimientosClienteDetalle",
                column: "MovimientoClienteAplicadoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosClienteDetalle_MovimientoClienteId",
                table: "MovimientosClienteDetalle",
                column: "MovimientoClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosClienteDetalle_TipoOrigen_ClaveOrigen",
                table: "MovimientosClienteDetalle",
                columns: new[] { "TipoOrigen", "ClaveOrigen" });

            // Documento posteado y libro de clientes append-only (Task 6.3): reutilizan libro_inventario_append_only() de
            // AddLibroInventario (versión vigente de PermitirContabilizacionCosto), que rechaza DELETE y todo UPDATE en cualquier
            // tabla distinta de MovimientosProducto/MovimientosValor; TRUNCATE, con un trigger de sentencia aparte (no dispara
            // los de fila). El saldo y el importe restante se derivan del detalle: nada de esto se actualiza nunca (D3).
            foreach (var tabla in TablasAppendOnly)
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER "TR_{tabla}_AppendOnly" BEFORE UPDATE OR DELETE ON "{tabla}"
                        FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
                    """);
                migrationBuilder.Sql($"""
                    CREATE TRIGGER "TR_{tabla}_NoTruncate" BEFORE TRUNCATE ON "{tabla}"
                        FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DESTRUCTIVO: borra las facturas posteadas y el libro de clientes.
            foreach (var tabla in TablasAppendOnly)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"TR_{tabla}_NoTruncate\" ON \"{tabla}\";");
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"TR_{tabla}_AppendOnly\" ON \"{tabla}\";");
            }

            migrationBuilder.DropTable(
                name: "LineasFacturaVenta");

            migrationBuilder.DropTable(
                name: "LineasIvaFacturaVenta");

            migrationBuilder.DropTable(
                name: "MovimientosClienteDetalle");

            migrationBuilder.DropTable(
                name: "FacturasVenta");

            migrationBuilder.DropTable(
                name: "MovimientosCliente");
        }
    }
}
