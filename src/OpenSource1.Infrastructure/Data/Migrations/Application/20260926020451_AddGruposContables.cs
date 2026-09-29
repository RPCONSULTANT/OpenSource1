using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddGruposContables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrupoClienteContableId",
                table: "SociosNegocio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoIvaNegocioId",
                table: "SociosNegocio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoNegocioId",
                table: "SociosNegocio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoInventarioId",
                table: "Productos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoIvaProductoId",
                table: "Productos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrupoProductoId",
                table: "Productos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GruposClienteContable",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaCxCId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaDescuentoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CuentaInteresId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposClienteContable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GruposClienteContable_CuentasContables_CuentaCxCId",
                        column: x => x.CuentaCxCId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposClienteContable_CuentasContables_CuentaDescuentoId",
                        column: x => x.CuentaDescuentoId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposClienteContable_CuentasContables_CuentaInteresId",
                        column: x => x.CuentaInteresId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GruposInventario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposInventario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposIvaNegocio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposIvaNegocio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposIvaProducto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposIvaProducto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposNegocio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposNegocio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposProducto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposProducto", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "GruposClienteContable",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "CuentaCxCId", "CuentaDescuentoId", "CuentaInteresId", "DeletedAtUtc", "DeletedBy", "Descripcion", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("f2000000-0000-0000-0000-000000000010"), "GENERAL", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", new Guid("f1000000-0000-0000-0000-000000000003"), new Guid("f1000000-0000-0000-0000-000000000009"), null, null, null, "Clientes en general", null, null });

            migrationBuilder.InsertData(
                table: "GruposInventario",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("f2000000-0000-0000-0000-000000000009"), "GENERAL", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Inventario general", null, null });

            migrationBuilder.InsertData(
                table: "GruposIvaNegocio",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("f2000000-0000-0000-0000-000000000005"), "ITBIS18", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Sujeto a ITBIS 18%", null, null },
                    { new Guid("f2000000-0000-0000-0000-000000000006"), "EXENTO", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Exento de ITBIS", null, null }
                });

            migrationBuilder.InsertData(
                table: "GruposIvaProducto",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("f2000000-0000-0000-0000-000000000007"), "ITBIS18", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Gravado con ITBIS 18%", null, null },
                    { new Guid("f2000000-0000-0000-0000-000000000008"), "EXENTO", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Exento de ITBIS", null, null }
                });

            migrationBuilder.InsertData(
                table: "GruposNegocio",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("f2000000-0000-0000-0000-000000000001"), "NACIONAL", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Socios nacionales", null, null },
                    { new Guid("f2000000-0000-0000-0000-000000000002"), "EXTERIOR", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Socios del exterior", null, null }
                });

            migrationBuilder.InsertData(
                table: "GruposProducto",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("f2000000-0000-0000-0000-000000000003"), "BIENES", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Bienes", null, null },
                    { new Guid("f2000000-0000-0000-0000-000000000004"), "SERVICIOS", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Servicios", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SociosNegocio_GrupoClienteContableId",
                table: "SociosNegocio",
                column: "GrupoClienteContableId");

            migrationBuilder.CreateIndex(
                name: "IX_SociosNegocio_GrupoIvaNegocioId",
                table: "SociosNegocio",
                column: "GrupoIvaNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_SociosNegocio_GrupoNegocioId",
                table: "SociosNegocio",
                column: "GrupoNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_GrupoInventarioId",
                table: "Productos",
                column: "GrupoInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_GrupoIvaProductoId",
                table: "Productos",
                column: "GrupoIvaProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_GrupoProductoId",
                table: "Productos",
                column: "GrupoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposClienteContable_Codigo",
                table: "GruposClienteContable",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GruposClienteContable_CuentaCxCId",
                table: "GruposClienteContable",
                column: "CuentaCxCId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposClienteContable_CuentaDescuentoId",
                table: "GruposClienteContable",
                column: "CuentaDescuentoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposClienteContable_CuentaInteresId",
                table: "GruposClienteContable",
                column: "CuentaInteresId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposInventario_Codigo",
                table: "GruposInventario",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GruposIvaNegocio_Codigo",
                table: "GruposIvaNegocio",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GruposIvaProducto_Codigo",
                table: "GruposIvaProducto",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GruposNegocio_Codigo",
                table: "GruposNegocio",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GruposProducto_Codigo",
                table: "GruposProducto",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_GruposInventario_GrupoInventarioId",
                table: "Productos",
                column: "GrupoInventarioId",
                principalTable: "GruposInventario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_GruposIvaProducto_GrupoIvaProductoId",
                table: "Productos",
                column: "GrupoIvaProductoId",
                principalTable: "GruposIvaProducto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_GruposProducto_GrupoProductoId",
                table: "Productos",
                column: "GrupoProductoId",
                principalTable: "GruposProducto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SociosNegocio_GruposClienteContable_GrupoClienteContableId",
                table: "SociosNegocio",
                column: "GrupoClienteContableId",
                principalTable: "GruposClienteContable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SociosNegocio_GruposIvaNegocio_GrupoIvaNegocioId",
                table: "SociosNegocio",
                column: "GrupoIvaNegocioId",
                principalTable: "GruposIvaNegocio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SociosNegocio_GruposNegocio_GrupoNegocioId",
                table: "SociosNegocio",
                column: "GrupoNegocioId",
                principalTable: "GruposNegocio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill (desviación "Grupos en maestros" de la Fase 5): los productos y socios que ya existían reciben los
            // grupos semilla por defecto (BIENES / ITBIS18 / GENERAL y NACIONAL / ITBIS18 / GENERAL). Incluye las filas
            // borradas lógicamente (una restauración no debe resucitar un registro sin clasificar). Solo rellena los nulos:
            // reejecutar Up() tras un Down() no pisa nada, porque Down() elimina las columnas. Ids literales (no
            // GrupoContableIds) para que la migración no cambie de significado si las constantes se tocaran algún día.
            migrationBuilder.Sql("""
                UPDATE "Productos"
                SET "GrupoProductoId" = COALESCE("GrupoProductoId", 'f2000000-0000-0000-0000-000000000003'),
                    "GrupoIvaProductoId" = COALESCE("GrupoIvaProductoId", 'f2000000-0000-0000-0000-000000000007'),
                    "GrupoInventarioId" = COALESCE("GrupoInventarioId", 'f2000000-0000-0000-0000-000000000009')
                WHERE "GrupoProductoId" IS NULL OR "GrupoIvaProductoId" IS NULL OR "GrupoInventarioId" IS NULL;

                UPDATE "SociosNegocio"
                SET "GrupoNegocioId" = COALESCE("GrupoNegocioId", 'f2000000-0000-0000-0000-000000000001'),
                    "GrupoIvaNegocioId" = COALESCE("GrupoIvaNegocioId", 'f2000000-0000-0000-0000-000000000005'),
                    "GrupoClienteContableId" = COALESCE("GrupoClienteContableId", 'f2000000-0000-0000-0000-000000000010')
                WHERE "GrupoNegocioId" IS NULL OR "GrupoIvaNegocioId" IS NULL OR "GrupoClienteContableId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Productos_GruposInventario_GrupoInventarioId",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_GruposIvaProducto_GrupoIvaProductoId",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_GruposProducto_GrupoProductoId",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_SociosNegocio_GruposClienteContable_GrupoClienteContableId",
                table: "SociosNegocio");

            migrationBuilder.DropForeignKey(
                name: "FK_SociosNegocio_GruposIvaNegocio_GrupoIvaNegocioId",
                table: "SociosNegocio");

            migrationBuilder.DropForeignKey(
                name: "FK_SociosNegocio_GruposNegocio_GrupoNegocioId",
                table: "SociosNegocio");

            migrationBuilder.DropTable(
                name: "GruposClienteContable");

            migrationBuilder.DropTable(
                name: "GruposInventario");

            migrationBuilder.DropTable(
                name: "GruposIvaNegocio");

            migrationBuilder.DropTable(
                name: "GruposIvaProducto");

            migrationBuilder.DropTable(
                name: "GruposNegocio");

            migrationBuilder.DropTable(
                name: "GruposProducto");

            migrationBuilder.DropIndex(
                name: "IX_SociosNegocio_GrupoClienteContableId",
                table: "SociosNegocio");

            migrationBuilder.DropIndex(
                name: "IX_SociosNegocio_GrupoIvaNegocioId",
                table: "SociosNegocio");

            migrationBuilder.DropIndex(
                name: "IX_SociosNegocio_GrupoNegocioId",
                table: "SociosNegocio");

            migrationBuilder.DropIndex(
                name: "IX_Productos_GrupoInventarioId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_GrupoIvaProductoId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_GrupoProductoId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "GrupoClienteContableId",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "GrupoIvaNegocioId",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "GrupoNegocioId",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "GrupoInventarioId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "GrupoIvaProductoId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "GrupoProductoId",
                table: "Productos");
        }
    }
}
