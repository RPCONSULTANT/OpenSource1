using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddFacturasVentaBorrador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacturasVentaBorrador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    Estado = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_FacturasVentaBorrador", x => x.Id);
                    table.CheckConstraint("CK_FacturasVentaBorrador_Estado", "\"Estado\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_FacturasVentaBorrador_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVentaBorrador_GruposClienteContable_GrupoClienteCon~",
                        column: x => x.GrupoClienteContableId,
                        principalTable: "GruposClienteContable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVentaBorrador_GruposIvaNegocio_GrupoIvaNegocioId",
                        column: x => x.GrupoIvaNegocioId,
                        principalTable: "GruposIvaNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVentaBorrador_GruposNegocio_GrupoNegocioId",
                        column: x => x.GrupoNegocioId,
                        principalTable: "GruposNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVentaBorrador_SociosNegocio_SocioNegocioFacturarAId",
                        column: x => x.SocioNegocioFacturarAId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVentaBorrador_SociosNegocio_SocioNegocioId",
                        column: x => x.SocioNegocioId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacturasVentaBorrador_TerminosPago_TerminoPagoId",
                        column: x => x.TerminoPagoId,
                        principalTable: "TerminosPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasFacturaVentaBorrador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacturaVentaBorradorId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_LineasFacturaVentaBorrador", x => x.Id);
                    table.CheckConstraint("CK_LineasFacturaVentaBorrador_Cantidad", "(\"Tipo\" = 3 AND \"Cantidad\" = 0) OR (\"Tipo\" <> 3 AND \"Cantidad\" > 0)");
                    table.CheckConstraint("CK_LineasFacturaVentaBorrador_Referencia", "(\"Tipo\" = 1 AND \"ProductoId\" IS NOT NULL AND \"CuentaContableId\" IS NULL) OR (\"Tipo\" = 2 AND \"CuentaContableId\" IS NOT NULL AND \"ProductoId\" IS NULL) OR (\"Tipo\" = 3 AND \"ProductoId\" IS NULL AND \"CuentaContableId\" IS NULL)");
                    table.CheckConstraint("CK_LineasFacturaVentaBorrador_Tipo", "\"Tipo\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_LineasFacturaVentaBorrador_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVentaBorrador_CuentasContables_CuentaContableId",
                        column: x => x.CuentaContableId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVentaBorrador_FacturasVentaBorrador_FacturaVen~",
                        column: x => x.FacturaVentaBorradorId,
                        principalTable: "FacturasVentaBorrador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVentaBorrador_GruposInventario_GrupoInventario~",
                        column: x => x.GrupoInventarioId,
                        principalTable: "GruposInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVentaBorrador_GruposIvaProducto_GrupoIvaProduc~",
                        column: x => x.GrupoIvaProductoId,
                        principalTable: "GruposIvaProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVentaBorrador_GruposProducto_GrupoProductoId",
                        column: x => x.GrupoProductoId,
                        principalTable: "GruposProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVentaBorrador_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasFacturaVentaBorrador_UnidadesMedida_UnidadMedidaId",
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
                    { new Guid("e1000000-0000-0000-0000-000000000005"), "FV-BORR", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Borradores de factura de venta", true, false, null, null },
                    { new Guid("e1000000-0000-0000-0000-000000000007"), "FV", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Facturas de venta", false, false, null, null }
                });

            migrationBuilder.InsertData(
                table: "LineasSerie",
                columns: new[] { "Id", "Bloqueada", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "FechaInicial", "Incremento", "NumeroFinal", "NumeroInicial", "SerieId", "UltimoNumeroUsado", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("e1000000-0000-0000-0000-000000000006"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new DateOnly(2020, 1, 1), 1, "99999999", "00000001", new Guid("e1000000-0000-0000-0000-000000000005"), "00000000", null, null },
                    { new Guid("e1000000-0000-0000-0000-000000000008"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new DateOnly(2020, 1, 1), 1, "99999999", "00000001", new Guid("e1000000-0000-0000-0000-000000000007"), "00000000", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_AlmacenId",
                table: "FacturasVentaBorrador",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_CreatedAtUtc",
                table: "FacturasVentaBorrador",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_GrupoClienteContableId",
                table: "FacturasVentaBorrador",
                column: "GrupoClienteContableId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_GrupoIvaNegocioId",
                table: "FacturasVentaBorrador",
                column: "GrupoIvaNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_GrupoNegocioId",
                table: "FacturasVentaBorrador",
                column: "GrupoNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_Numero",
                table: "FacturasVentaBorrador",
                column: "Numero",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_SocioNegocioFacturarAId",
                table: "FacturasVentaBorrador",
                column: "SocioNegocioFacturarAId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_SocioNegocioId",
                table: "FacturasVentaBorrador",
                column: "SocioNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_TerminoPagoId",
                table: "FacturasVentaBorrador",
                column: "TerminoPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_AlmacenId",
                table: "LineasFacturaVentaBorrador",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_CreatedAtUtc",
                table: "LineasFacturaVentaBorrador",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_CuentaContableId",
                table: "LineasFacturaVentaBorrador",
                column: "CuentaContableId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_FacturaVentaBorradorId_NumeroLin~",
                table: "LineasFacturaVentaBorrador",
                columns: new[] { "FacturaVentaBorradorId", "NumeroLinea" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_GrupoInventarioId",
                table: "LineasFacturaVentaBorrador",
                column: "GrupoInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_GrupoIvaProductoId",
                table: "LineasFacturaVentaBorrador",
                column: "GrupoIvaProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_GrupoProductoId",
                table: "LineasFacturaVentaBorrador",
                column: "GrupoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_ProductoId",
                table: "LineasFacturaVentaBorrador",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasFacturaVentaBorrador_UnidadMedidaId",
                table: "LineasFacturaVentaBorrador",
                column: "UnidadMedidaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LineasFacturaVentaBorrador");

            migrationBuilder.DropTable(
                name: "FacturasVentaBorrador");

            migrationBuilder.DeleteData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000007"));
        }
    }
}
