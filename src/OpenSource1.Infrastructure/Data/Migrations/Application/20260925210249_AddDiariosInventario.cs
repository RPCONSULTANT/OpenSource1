using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddDiariosInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlantillasDiario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<short>(type: "smallint", nullable: false),
                    SerieId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_PlantillasDiario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlantillasDiario_Series_SerieId",
                        column: x => x.SerieId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotesDiario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlantillaDiarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SerieId = table.Column<Guid>(type: "uuid", nullable: true),
                    Bloqueado = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LotesDiario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotesDiario_PlantillasDiario_PlantillaDiarioId",
                        column: x => x.PlantillaDiarioId,
                        principalTable: "PlantillasDiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesDiario_Series_SerieId",
                        column: x => x.SerieId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasDiario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteDiarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroLinea = table.Column<int>(type: "integer", nullable: false),
                    FechaRegistro = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TipoMovimiento = table.Column<short>(type: "smallint", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlmacenDestinoId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnidadMedidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CantidadPorUnidadMedida = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ImporteCosto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_LineasDiario", x => x.Id);
                    table.CheckConstraint("CK_LineasDiario_Cantidad_Positiva", "\"Cantidad\" > 0");
                    table.ForeignKey(
                        name: "FK_LineasDiario_Almacenes_AlmacenDestinoId",
                        column: x => x.AlmacenDestinoId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasDiario_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasDiario_LotesDiario_LoteDiarioId",
                        column: x => x.LoteDiarioId,
                        principalTable: "LotesDiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasDiario_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasDiario_UnidadesMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalTable: "UnidadesMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Series",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "PermiteHuecos", "PorDefecto", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("e1000000-0000-0000-0000-000000000001"), "DIARIO-INV", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Diarios de inventario", false, false, null, null });

            migrationBuilder.InsertData(
                table: "LineasSerie",
                columns: new[] { "Id", "Bloqueada", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "FechaInicial", "Incremento", "NumeroFinal", "NumeroInicial", "SerieId", "UltimoNumeroUsado", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("e1000000-0000-0000-0000-000000000002"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new DateOnly(2020, 1, 1), 1, "999999", "000001", new Guid("e1000000-0000-0000-0000-000000000001"), "000000", null, null });

            migrationBuilder.InsertData(
                table: "PlantillasDiario",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Nombre", "SerieId", "Tipo", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("d1000000-0000-0000-0000-000000000001"), "ARTICULO", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Diario de artículos", new Guid("e1000000-0000-0000-0000-000000000001"), (short)1, null, null },
                    { new Guid("d1000000-0000-0000-0000-000000000002"), "RECLASIF", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Diario de reclasificación", new Guid("e1000000-0000-0000-0000-000000000001"), (short)2, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineasDiario_AlmacenDestinoId",
                table: "LineasDiario",
                column: "AlmacenDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasDiario_AlmacenId",
                table: "LineasDiario",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasDiario_CreatedAtUtc",
                table: "LineasDiario",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LineasDiario_LoteDiarioId_NumeroLinea",
                table: "LineasDiario",
                columns: new[] { "LoteDiarioId", "NumeroLinea" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LineasDiario_ProductoId",
                table: "LineasDiario",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasDiario_UnidadMedidaId",
                table: "LineasDiario",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesDiario_CreatedAtUtc",
                table: "LotesDiario",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LotesDiario_PlantillaDiarioId_Codigo",
                table: "LotesDiario",
                columns: new[] { "PlantillaDiarioId", "Codigo" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_LotesDiario_SerieId",
                table: "LotesDiario",
                column: "SerieId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasDiario_Codigo",
                table: "PlantillasDiario",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasDiario_CreatedAtUtc",
                table: "PlantillasDiario",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasDiario_SerieId",
                table: "PlantillasDiario",
                column: "SerieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LineasDiario");

            migrationBuilder.DropTable(
                name: "LotesDiario");

            migrationBuilder.DropTable(
                name: "PlantillasDiario");

            migrationBuilder.DeleteData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000001"));
        }
    }
}
