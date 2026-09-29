using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddSetupsContables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SetupsContableGeneral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoNegocioId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaVentasId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaCostoVentasId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaDescuentoVentasId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaAjusteInventarioId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SetupsContableGeneral", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupsContableGeneral_CuentasContables_CuentaAjusteInventar~",
                        column: x => x.CuentaAjusteInventarioId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsContableGeneral_CuentasContables_CuentaCostoVentasId",
                        column: x => x.CuentaCostoVentasId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsContableGeneral_CuentasContables_CuentaDescuentoVenta~",
                        column: x => x.CuentaDescuentoVentasId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsContableGeneral_CuentasContables_CuentaVentasId",
                        column: x => x.CuentaVentasId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsContableGeneral_GruposNegocio_GrupoNegocioId",
                        column: x => x.GrupoNegocioId,
                        principalTable: "GruposNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsContableGeneral_GruposProducto_GrupoProductoId",
                        column: x => x.GrupoProductoId,
                        principalTable: "GruposProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SetupsInventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoInventarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaInventarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaAjusteInventarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaVariacionCostoId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_SetupsInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupsInventario_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsInventario_CuentasContables_CuentaAjusteInventarioId",
                        column: x => x.CuentaAjusteInventarioId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsInventario_CuentasContables_CuentaInventarioId",
                        column: x => x.CuentaInventarioId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsInventario_CuentasContables_CuentaVariacionCostoId",
                        column: x => x.CuentaVariacionCostoId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsInventario_GruposInventario_GrupoInventarioId",
                        column: x => x.GrupoInventarioId,
                        principalTable: "GruposInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SetupsIva",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoIvaNegocioId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoIvaProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PorcentajeIva = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
                    CuentaIvaVentasId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaIvaComprasId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentificadorIva = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoCalculoIva = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_SetupsIva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetupsIva_CuentasContables_CuentaIvaComprasId",
                        column: x => x.CuentaIvaComprasId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsIva_CuentasContables_CuentaIvaVentasId",
                        column: x => x.CuentaIvaVentasId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsIva_GruposIvaNegocio_GrupoIvaNegocioId",
                        column: x => x.GrupoIvaNegocioId,
                        principalTable: "GruposIvaNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SetupsIva_GruposIvaProducto_GrupoIvaProductoId",
                        column: x => x.GrupoIvaProductoId,
                        principalTable: "GruposIvaProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SetupsContableGeneral",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "CuentaAjusteInventarioId", "CuentaCostoVentasId", "CuentaDescuentoVentasId", "CuentaVentasId", "DeletedAtUtc", "DeletedBy", "GrupoNegocioId", "GrupoProductoId", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("f3000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", new Guid("f1000000-0000-0000-0000-000000000012"), new Guid("f1000000-0000-0000-0000-000000000011"), new Guid("f1000000-0000-0000-0000-000000000009"), new Guid("f1000000-0000-0000-0000-000000000008"), null, null, new Guid("f2000000-0000-0000-0000-000000000001"), new Guid("f2000000-0000-0000-0000-000000000003"), null, null },
                    { new Guid("f3000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", new Guid("f1000000-0000-0000-0000-000000000012"), new Guid("f1000000-0000-0000-0000-000000000011"), new Guid("f1000000-0000-0000-0000-000000000009"), new Guid("f1000000-0000-0000-0000-000000000008"), null, null, new Guid("f2000000-0000-0000-0000-000000000001"), new Guid("f2000000-0000-0000-0000-000000000004"), null, null },
                    { new Guid("f3000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", new Guid("f1000000-0000-0000-0000-000000000012"), new Guid("f1000000-0000-0000-0000-000000000011"), new Guid("f1000000-0000-0000-0000-000000000009"), new Guid("f1000000-0000-0000-0000-000000000008"), null, null, null, new Guid("f2000000-0000-0000-0000-000000000003"), null, null },
                    { new Guid("f3000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", new Guid("f1000000-0000-0000-0000-000000000012"), new Guid("f1000000-0000-0000-0000-000000000011"), new Guid("f1000000-0000-0000-0000-000000000009"), new Guid("f1000000-0000-0000-0000-000000000008"), null, null, null, new Guid("f2000000-0000-0000-0000-000000000004"), null, null }
                });

            migrationBuilder.InsertData(
                table: "SetupsInventario",
                columns: new[] { "Id", "AlmacenId", "CreatedAtUtc", "CreatedBy", "CuentaAjusteInventarioId", "CuentaInventarioId", "CuentaVariacionCostoId", "DeletedAtUtc", "DeletedBy", "GrupoInventarioId", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("f3000000-0000-0000-0000-000000000009"), null, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", new Guid("f1000000-0000-0000-0000-000000000012"), new Guid("f1000000-0000-0000-0000-000000000004"), new Guid("f1000000-0000-0000-0000-000000000012"), null, null, new Guid("f2000000-0000-0000-0000-000000000009"), null, null });

            migrationBuilder.InsertData(
                table: "SetupsIva",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "CuentaIvaComprasId", "CuentaIvaVentasId", "DeletedAtUtc", "DeletedBy", "GrupoIvaNegocioId", "GrupoIvaProductoId", "IdentificadorIva", "PorcentajeIva", "TipoCalculoIva", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("f3000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, new Guid("f1000000-0000-0000-0000-000000000006"), null, null, new Guid("f2000000-0000-0000-0000-000000000005"), new Guid("f2000000-0000-0000-0000-000000000007"), "ITBIS18", 18m, (short)1, null, null },
                    { new Guid("f3000000-0000-0000-0000-000000000006"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, new Guid("f1000000-0000-0000-0000-000000000006"), null, null, new Guid("f2000000-0000-0000-0000-000000000005"), new Guid("f2000000-0000-0000-0000-000000000008"), "EXENTO", 0m, (short)2, null, null },
                    { new Guid("f3000000-0000-0000-0000-000000000007"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, new Guid("f1000000-0000-0000-0000-000000000006"), null, null, new Guid("f2000000-0000-0000-0000-000000000006"), new Guid("f2000000-0000-0000-0000-000000000007"), "EXENTO", 0m, (short)2, null, null },
                    { new Guid("f3000000-0000-0000-0000-000000000008"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, new Guid("f1000000-0000-0000-0000-000000000006"), null, null, new Guid("f2000000-0000-0000-0000-000000000006"), new Guid("f2000000-0000-0000-0000-000000000008"), "EXENTO", 0m, (short)2, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SetupsContableGeneral_CuentaAjusteInventarioId",
                table: "SetupsContableGeneral",
                column: "CuentaAjusteInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsContableGeneral_CuentaCostoVentasId",
                table: "SetupsContableGeneral",
                column: "CuentaCostoVentasId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsContableGeneral_CuentaDescuentoVentasId",
                table: "SetupsContableGeneral",
                column: "CuentaDescuentoVentasId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsContableGeneral_CuentaVentasId",
                table: "SetupsContableGeneral",
                column: "CuentaVentasId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsContableGeneral_GrupoNegocioId_GrupoProductoId",
                table: "SetupsContableGeneral",
                columns: new[] { "GrupoNegocioId", "GrupoProductoId" },
                unique: true,
                filter: "\"IsDeleted\" = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_SetupsContableGeneral_GrupoProductoId",
                table: "SetupsContableGeneral",
                column: "GrupoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsInventario_AlmacenId_GrupoInventarioId",
                table: "SetupsInventario",
                columns: new[] { "AlmacenId", "GrupoInventarioId" },
                unique: true,
                filter: "\"IsDeleted\" = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_SetupsInventario_CuentaAjusteInventarioId",
                table: "SetupsInventario",
                column: "CuentaAjusteInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsInventario_CuentaInventarioId",
                table: "SetupsInventario",
                column: "CuentaInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsInventario_CuentaVariacionCostoId",
                table: "SetupsInventario",
                column: "CuentaVariacionCostoId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsInventario_GrupoInventarioId",
                table: "SetupsInventario",
                column: "GrupoInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsIva_CuentaIvaComprasId",
                table: "SetupsIva",
                column: "CuentaIvaComprasId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsIva_CuentaIvaVentasId",
                table: "SetupsIva",
                column: "CuentaIvaVentasId");

            migrationBuilder.CreateIndex(
                name: "IX_SetupsIva_GrupoIvaNegocioId_GrupoIvaProductoId",
                table: "SetupsIva",
                columns: new[] { "GrupoIvaNegocioId", "GrupoIvaProductoId" },
                unique: true,
                filter: "\"IsDeleted\" = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_SetupsIva_GrupoIvaProductoId",
                table: "SetupsIva",
                column: "GrupoIvaProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetupsContableGeneral");

            migrationBuilder.DropTable(
                name: "SetupsInventario");

            migrationBuilder.DropTable(
                name: "SetupsIva");
        }
    }
}
