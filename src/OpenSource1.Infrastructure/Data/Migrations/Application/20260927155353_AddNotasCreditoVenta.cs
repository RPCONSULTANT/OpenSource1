using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <summary>
    /// Notas de crédito de venta (Task 8.6): borradores (maestros con <c>xmin</c>), documento posteado (<c>NotasCreditoVenta</c>,
    /// <c>LineasNotaCreditoVenta</c>, <c>LineasIvaNotaCreditoVenta</c>) append-only con el trigger <c>libro_inventario_append_only()</c>,
    /// y las series <c>NC-BORR</c> (con huecos) y <c>NC</c> (sin huecos).
    /// </summary>
    public partial class AddNotasCreditoVenta : Migration
    {
        private static readonly string[] TablasAppendOnly = ["NotasCreditoVenta", "LineasNotaCreditoVenta", "LineasIvaNotaCreditoVenta"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotasCreditoVenta",
                columns: table => new
                {
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroBorrador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FacturaVentaNumero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    GrupoNegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoIvaNegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoClienteContableId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaCxCId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_NotasCreditoVenta", x => x.Numero);
                    table.CheckConstraint("CK_NotasCreditoVenta_Redondeo", "\"ImporteSinIva\" = ROUND(\"ImporteSinIva\", 2) AND \"ImporteIva\" = ROUND(\"ImporteIva\", 2) AND \"ImporteTotal\" = ROUND(\"ImporteTotal\", 2)");
                    table.CheckConstraint("CK_NotasCreditoVenta_Total", "\"ImporteTotal\" = \"ImporteSinIva\" + \"ImporteIva\"");
                    table.CheckConstraint("CK_NotasCreditoVenta_TotalNoNegativo", "\"ImporteTotal\" >= 0");
                    table.ForeignKey(
                        name: "FK_NotasCreditoVenta_CuentasContables_CuentaCxCId",
                        column: x => x.CuentaCxCId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVenta_FacturasVenta_FacturaVentaNumero",
                        column: x => x.FacturaVentaNumero,
                        principalTable: "FacturasVenta",
                        principalColumn: "Numero",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVenta_GruposClienteContable_GrupoClienteContabl~",
                        column: x => x.GrupoClienteContableId,
                        principalTable: "GruposClienteContable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVenta_GruposIvaNegocio_GrupoIvaNegocioId",
                        column: x => x.GrupoIvaNegocioId,
                        principalTable: "GruposIvaNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVenta_GruposNegocio_GrupoNegocioId",
                        column: x => x.GrupoNegocioId,
                        principalTable: "GruposNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVenta_RegistrosContables_RegistroContableId",
                        column: x => x.RegistroContableId,
                        principalTable: "RegistrosContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVenta_SociosNegocio_SocioNegocioFacturarAId",
                        column: x => x.SocioNegocioFacturarAId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVenta_SociosNegocio_SocioNegocioId",
                        column: x => x.SocioNegocioId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotasCreditoVentaBorrador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FacturaVentaNumero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    GrupoNegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoIvaNegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoClienteContableId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaCxCId = table.Column<Guid>(type: "uuid", nullable: true),
                    Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasCreditoVentaBorrador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVentaBorrador_CuentasContables_CuentaCxCId",
                        column: x => x.CuentaCxCId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVentaBorrador_FacturasVenta_FacturaVentaNumero",
                        column: x => x.FacturaVentaNumero,
                        principalTable: "FacturasVenta",
                        principalColumn: "Numero",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVentaBorrador_GruposClienteContable_GrupoClient~",
                        column: x => x.GrupoClienteContableId,
                        principalTable: "GruposClienteContable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVentaBorrador_GruposIvaNegocio_GrupoIvaNegocioId",
                        column: x => x.GrupoIvaNegocioId,
                        principalTable: "GruposIvaNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVentaBorrador_GruposNegocio_GrupoNegocioId",
                        column: x => x.GrupoNegocioId,
                        principalTable: "GruposNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVentaBorrador_SociosNegocio_SocioNegocioFactura~",
                        column: x => x.SocioNegocioFacturarAId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCreditoVentaBorrador_SociosNegocio_SocioNegocioId",
                        column: x => x.SocioNegocioId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasIvaNotaCreditoVenta",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    NotaCreditoVentaNumero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IdentificadorIva = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PorcentajeIva = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
                    BaseImponible = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImporteIva = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CuentaIvaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasIvaNotaCreditoVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasIvaNotaCreditoVenta_CuentasContables_CuentaIvaId",
                        column: x => x.CuentaIvaId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasIvaNotaCreditoVenta_NotasCreditoVenta_NotaCreditoVent~",
                        column: x => x.NotaCreditoVentaNumero,
                        principalTable: "NotasCreditoVenta",
                        principalColumn: "Numero",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasNotaCreditoVenta",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    NotaCreditoVentaNumero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NumeroLinea = table.Column<int>(type: "integer", nullable: false),
                    LineaFacturaVentaId = table.Column<long>(type: "bigint", nullable: false),
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
                    DevolverInventario = table.Column<bool>(type: "boolean", nullable: false),
                    MovimientoProductoId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasNotaCreditoVenta", x => x.Id);
                    table.CheckConstraint("CK_LineasNotaCreditoVenta_Cantidad", "\"Cantidad\" > 0");
                    table.CheckConstraint("CK_LineasNotaCreditoVenta_Devolucion", "(\"DevolverInventario\" = true AND \"Tipo\" = 1 AND \"MovimientoProductoId\" IS NOT NULL) OR (\"DevolverInventario\" = false AND \"MovimientoProductoId\" IS NULL)");
                    table.CheckConstraint("CK_LineasNotaCreditoVenta_Tipo", "\"Tipo\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_CuentasContables_CuentaContableId",
                        column: x => x.CuentaContableId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_GruposInventario_GrupoInventarioId",
                        column: x => x.GrupoInventarioId,
                        principalTable: "GruposInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_GruposIvaProducto_GrupoIvaProductoId",
                        column: x => x.GrupoIvaProductoId,
                        principalTable: "GruposIvaProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_GruposProducto_GrupoProductoId",
                        column: x => x.GrupoProductoId,
                        principalTable: "GruposProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_LineasFacturaVenta_LineaFacturaVenta~",
                        column: x => x.LineaFacturaVentaId,
                        principalTable: "LineasFacturaVenta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_MovimientosProducto_MovimientoProduc~",
                        column: x => x.MovimientoProductoId,
                        principalTable: "MovimientosProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_NotasCreditoVenta_NotaCreditoVentaNu~",
                        column: x => x.NotaCreditoVentaNumero,
                        principalTable: "NotasCreditoVenta",
                        principalColumn: "Numero",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVenta_UnidadesMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalTable: "UnidadesMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasNotaCreditoVentaBorrador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotaCreditoVentaBorradorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineaFacturaVentaId = table.Column<long>(type: "bigint", nullable: false),
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
                    DevolverInventario = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasNotaCreditoVentaBorrador", x => x.Id);
                    table.CheckConstraint("CK_LineasNotaCreditoVentaBorrador_Cantidad", "\"Cantidad\" > 0");
                    table.CheckConstraint("CK_LineasNotaCreditoVentaBorrador_Devolucion", "\"Tipo\" = 1 OR \"DevolverInventario\" = false");
                    table.CheckConstraint("CK_LineasNotaCreditoVentaBorrador_Tipo", "\"Tipo\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_CuentasContables_CuentaConta~",
                        column: x => x.CuentaContableId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_GruposInventario_GrupoInvent~",
                        column: x => x.GrupoInventarioId,
                        principalTable: "GruposInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_GruposIvaProducto_GrupoIvaPr~",
                        column: x => x.GrupoIvaProductoId,
                        principalTable: "GruposIvaProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_GruposProducto_GrupoProducto~",
                        column: x => x.GrupoProductoId,
                        principalTable: "GruposProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_LineasFacturaVenta_LineaFact~",
                        column: x => x.LineaFacturaVentaId,
                        principalTable: "LineasFacturaVenta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_NotasCreditoVentaBorrador_No~",
                        column: x => x.NotaCreditoVentaBorradorId,
                        principalTable: "NotasCreditoVentaBorrador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasNotaCreditoVentaBorrador_UnidadesMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalTable: "UnidadesMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Series",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "PermiteHuecos", "PorDefecto", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("e1000000-0000-0000-0000-00000000000b"), "NC-BORR", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Borradores de nota de crédito de venta", true, false, null, null },
                    { new Guid("e1000000-0000-0000-0000-00000000000d"), "NC", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Notas de crédito de venta", false, false, null, null }
                });

            migrationBuilder.InsertData(
                table: "LineasSerie",
                columns: new[] { "Id", "Bloqueada", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "FechaInicial", "Incremento", "NumeroFinal", "NumeroInicial", "SerieId", "UltimoNumeroUsado", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("e1000000-0000-0000-0000-00000000000c"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new DateOnly(2020, 1, 1), 1, "99999999", "00000001", new Guid("e1000000-0000-0000-0000-00000000000b"), "00000000", null, null },
                    { new Guid("e1000000-0000-0000-0000-00000000000e"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new DateOnly(2020, 1, 1), 1, "99999999", "00000001", new Guid("e1000000-0000-0000-0000-00000000000d"), "00000000", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineasIvaNotaCreditoVenta_CuentaIvaId",
                table: "LineasIvaNotaCreditoVenta",
                column: "CuentaIvaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasIvaNotaCreditoVenta_NotaCreditoVentaNumero_IdentificadorIva",
                table: "LineasIvaNotaCreditoVenta",
                columns: new[] { "NotaCreditoVentaNumero", "IdentificadorIva" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_AlmacenId",
                table: "LineasNotaCreditoVenta",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_CuentaContableId",
                table: "LineasNotaCreditoVenta",
                column: "CuentaContableId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_GrupoInventarioId",
                table: "LineasNotaCreditoVenta",
                column: "GrupoInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_GrupoIvaProductoId",
                table: "LineasNotaCreditoVenta",
                column: "GrupoIvaProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_GrupoProductoId",
                table: "LineasNotaCreditoVenta",
                column: "GrupoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_LineaFacturaVentaId",
                table: "LineasNotaCreditoVenta",
                column: "LineaFacturaVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_MovimientoProductoId",
                table: "LineasNotaCreditoVenta",
                column: "MovimientoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_NotaCreditoVentaNumero_NumeroLinea",
                table: "LineasNotaCreditoVenta",
                columns: new[] { "NotaCreditoVentaNumero", "NumeroLinea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_ProductoId",
                table: "LineasNotaCreditoVenta",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVenta_UnidadMedidaId",
                table: "LineasNotaCreditoVenta",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_AlmacenId",
                table: "LineasNotaCreditoVentaBorrador",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_CreatedAtUtc",
                table: "LineasNotaCreditoVentaBorrador",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_CuentaContableId",
                table: "LineasNotaCreditoVentaBorrador",
                column: "CuentaContableId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_GrupoInventarioId",
                table: "LineasNotaCreditoVentaBorrador",
                column: "GrupoInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_GrupoIvaProductoId",
                table: "LineasNotaCreditoVentaBorrador",
                column: "GrupoIvaProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_GrupoProductoId",
                table: "LineasNotaCreditoVentaBorrador",
                column: "GrupoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_LineaFacturaVentaId",
                table: "LineasNotaCreditoVentaBorrador",
                column: "LineaFacturaVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_NotaCreditoVentaBorradorId_L~",
                table: "LineasNotaCreditoVentaBorrador",
                columns: new[] { "NotaCreditoVentaBorradorId", "LineaFacturaVentaId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_NotaCreditoVentaBorradorId_N~",
                table: "LineasNotaCreditoVentaBorrador",
                columns: new[] { "NotaCreditoVentaBorradorId", "NumeroLinea" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_ProductoId",
                table: "LineasNotaCreditoVentaBorrador",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasNotaCreditoVentaBorrador_UnidadMedidaId",
                table: "LineasNotaCreditoVentaBorrador",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_CuentaCxCId",
                table: "NotasCreditoVenta",
                column: "CuentaCxCId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_FacturaVentaNumero",
                table: "NotasCreditoVenta",
                column: "FacturaVentaNumero");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_FechaRegistro",
                table: "NotasCreditoVenta",
                column: "FechaRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_GrupoClienteContableId",
                table: "NotasCreditoVenta",
                column: "GrupoClienteContableId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_GrupoIvaNegocioId",
                table: "NotasCreditoVenta",
                column: "GrupoIvaNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_GrupoNegocioId",
                table: "NotasCreditoVenta",
                column: "GrupoNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_NumeroBorrador",
                table: "NotasCreditoVenta",
                column: "NumeroBorrador",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_RegistroContableId",
                table: "NotasCreditoVenta",
                column: "RegistroContableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_SocioNegocioFacturarAId_FechaRegistro",
                table: "NotasCreditoVenta",
                columns: new[] { "SocioNegocioFacturarAId", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVenta_SocioNegocioId_FechaRegistro",
                table: "NotasCreditoVenta",
                columns: new[] { "SocioNegocioId", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_CreatedAtUtc",
                table: "NotasCreditoVentaBorrador",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_CuentaCxCId",
                table: "NotasCreditoVentaBorrador",
                column: "CuentaCxCId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_FacturaVentaNumero",
                table: "NotasCreditoVentaBorrador",
                column: "FacturaVentaNumero");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_GrupoClienteContableId",
                table: "NotasCreditoVentaBorrador",
                column: "GrupoClienteContableId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_GrupoIvaNegocioId",
                table: "NotasCreditoVentaBorrador",
                column: "GrupoIvaNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_GrupoNegocioId",
                table: "NotasCreditoVentaBorrador",
                column: "GrupoNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_Numero",
                table: "NotasCreditoVentaBorrador",
                column: "Numero",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_SocioNegocioFacturarAId",
                table: "NotasCreditoVentaBorrador",
                column: "SocioNegocioFacturarAId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_SocioNegocioId",
                table: "NotasCreditoVentaBorrador",
                column: "SocioNegocioId");

            // Documento posteado append-only (mismo trigger y mismo patrón que AddFacturasVentaYLibroClientes): rechaza todo UPDATE y
            // DELETE de fila y, con un trigger de sentencia aparte, TRUNCATE.
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
            // DESTRUCTIVO: borra las notas de crédito posteadas (sus movimientos de inventario, cliente y contables quedan en los libros).
            foreach (var tabla in TablasAppendOnly)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"TR_{tabla}_NoTruncate\" ON \"{tabla}\";");
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS \"TR_{tabla}_AppendOnly\" ON \"{tabla}\";");
            }

            migrationBuilder.DropTable(
                name: "LineasIvaNotaCreditoVenta");

            migrationBuilder.DropTable(
                name: "LineasNotaCreditoVenta");

            migrationBuilder.DropTable(
                name: "LineasNotaCreditoVentaBorrador");

            migrationBuilder.DropTable(
                name: "NotasCreditoVenta");

            migrationBuilder.DropTable(
                name: "NotasCreditoVentaBorrador");

            migrationBuilder.DeleteData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000c"));

            migrationBuilder.DeleteData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000e"));

            migrationBuilder.DeleteData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000d"));
        }
    }
}
