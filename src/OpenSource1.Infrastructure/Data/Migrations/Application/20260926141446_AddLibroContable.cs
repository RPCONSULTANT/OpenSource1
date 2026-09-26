using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddLibroContable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosContables",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    NumeroRegistro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DesdeMovimiento = table.Column<long>(type: "bigint", nullable: false),
                    HastaMovimiento = table.Column<long>(type: "bigint", nullable: false),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoOrigen = table.Column<short>(type: "smallint", nullable: false),
                    ClaveOrigen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosContables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosContables",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    CuentaContableId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroCuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaRegistro = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoDocumento = table.Column<short>(type: "smallint", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Debito = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Credito = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RegistroContableId = table.Column<long>(type: "bigint", nullable: false),
                    SocioNegocioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoNegocioId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoIvaNegocioId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoIvaProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoOrigen = table.Column<short>(type: "smallint", nullable: false),
                    ClaveOrigen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosContables", x => x.Id);
                    table.CheckConstraint("CK_MovimientosContables_DebitoCredito", "\"Debito\" >= 0 AND \"Credito\" >= 0 AND \"Debito\" - \"Credito\" = \"Importe\" AND (\"Debito\" = 0 OR \"Credito\" = 0)");
                    table.CheckConstraint("CK_MovimientosContables_Importe_NoCero", "\"Importe\" <> 0");
                    table.ForeignKey(
                        name: "FK_MovimientosContables_CuentasContables_CuentaContableId",
                        column: x => x.CuentaContableId,
                        principalTable: "CuentasContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosContables_GruposIvaNegocio_GrupoIvaNegocioId",
                        column: x => x.GrupoIvaNegocioId,
                        principalTable: "GruposIvaNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosContables_GruposIvaProducto_GrupoIvaProductoId",
                        column: x => x.GrupoIvaProductoId,
                        principalTable: "GruposIvaProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosContables_GruposNegocio_GrupoNegocioId",
                        column: x => x.GrupoNegocioId,
                        principalTable: "GruposNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosContables_GruposProducto_GrupoProductoId",
                        column: x => x.GrupoProductoId,
                        principalTable: "GruposProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosContables_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosContables_RegistrosContables_RegistroContableId",
                        column: x => x.RegistroContableId,
                        principalTable: "RegistrosContables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosContables_SociosNegocio_SocioNegocioId",
                        column: x => x.SocioNegocioId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Series",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "PermiteHuecos", "PorDefecto", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("e1000000-0000-0000-0000-000000000003"), "CONTAB", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Registros contables", false, false, null, null });

            migrationBuilder.InsertData(
                table: "LineasSerie",
                columns: new[] { "Id", "Bloqueada", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "FechaInicial", "Incremento", "NumeroFinal", "NumeroInicial", "SerieId", "UltimoNumeroUsado", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("e1000000-0000-0000-0000-000000000004"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new DateOnly(2020, 1, 1), 1, "99999999", "00000001", new Guid("e1000000-0000-0000-0000-000000000003"), "00000000", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosValor_GrupoInventarioId",
                table: "MovimientosValor",
                column: "GrupoInventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosValor_GrupoNegocioId",
                table: "MovimientosValor",
                column: "GrupoNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosValor_GrupoProductoId",
                table: "MovimientosValor",
                column: "GrupoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_CuentaContableId_FechaRegistro",
                table: "MovimientosContables",
                columns: new[] { "CuentaContableId", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_GrupoIvaNegocioId",
                table: "MovimientosContables",
                column: "GrupoIvaNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_GrupoIvaProductoId",
                table: "MovimientosContables",
                column: "GrupoIvaProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_GrupoNegocioId",
                table: "MovimientosContables",
                column: "GrupoNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_GrupoProductoId",
                table: "MovimientosContables",
                column: "GrupoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_ProductoId",
                table: "MovimientosContables",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_RegistroContableId",
                table: "MovimientosContables",
                column: "RegistroContableId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_SocioNegocioId",
                table: "MovimientosContables",
                column: "SocioNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_TipoDocumento_NumeroDocumento",
                table: "MovimientosContables",
                columns: new[] { "TipoDocumento", "NumeroDocumento" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosContables_TipoOrigen_ClaveOrigen",
                table: "MovimientosContables",
                columns: new[] { "TipoOrigen", "ClaveOrigen" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosContables_NumeroRegistro",
                table: "RegistrosContables",
                column: "NumeroRegistro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosContables_TipoOrigen_ClaveOrigen",
                table: "RegistrosContables",
                columns: new[] { "TipoOrigen", "ClaveOrigen" });

            // Libro contable append-only (Task 5.5): reutiliza libro_inventario_append_only() de AddLibroInventario, que
            // rechaza DELETE y todo UPDATE en cualquier tabla distinta de MovimientosProducto; TRUNCATE, con un trigger de
            // sentencia aparte (no dispara los de fila).
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_MovimientosContables_AppendOnly" BEFORE UPDATE OR DELETE ON "MovimientosContables"
                    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_MovimientosContables_NoTruncate" BEFORE TRUNCATE ON "MovimientosContables"
                    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_RegistrosContables_AppendOnly" BEFORE UPDATE OR DELETE ON "RegistrosContables"
                    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_RegistrosContables_NoTruncate" BEFORE TRUNCATE ON "RegistrosContables"
                    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
                """);

            // Backfill de los grupos congelados de MovimientosValor (desviación de la Fase 5, D8). ÚNICA EXCEPCIÓN al
            // append-only del libro de valor: el trigger se desactiva SOLO aquí, dentro de la transacción de esta
            // migración (Up), y se reactiva en la misma transacción; si algo falla, el rollback deja el trigger activo.
            // Deja SOLO valores válidos antes de crear las FK de abajo: se conserva un grupo que ya exista en su catálogo;
            // cualquier otro valor (nulo o un uuid sin fila) se sustituye por el grupo del producto o, si el producto no
            // tiene, por la semilla (GENERAL / BIENES). El grupo de negocio sale del socio del movimiento de producto
            // (NACIONAL si el socio no tiene); sin socio queda NULL (comodín), igual que RegistrarAsync. Los grupos del
            // producto/socio ya son válidos por sus propias FK (AddGruposContables).
            migrationBuilder.Sql("""
                ALTER TABLE "MovimientosValor" DISABLE TRIGGER "TR_MovimientosValor_AppendOnly";

                UPDATE "MovimientosValor" mv SET
                    "GrupoInventarioId" = CASE
                        WHEN EXISTS (SELECT 1 FROM "GruposInventario" g WHERE g."Id" = mv."GrupoInventarioId") THEN mv."GrupoInventarioId"
                        ELSE COALESCE(
                            (SELECT p."GrupoInventarioId" FROM "Productos" p WHERE p."Id" = mv."ProductoId"),
                            (SELECT g."Id" FROM "GruposInventario" g WHERE g."Id" = 'f2000000-0000-0000-0000-000000000009'))
                    END,
                    "GrupoProductoId" = CASE
                        WHEN EXISTS (SELECT 1 FROM "GruposProducto" g WHERE g."Id" = mv."GrupoProductoId") THEN mv."GrupoProductoId"
                        ELSE COALESCE(
                            (SELECT p."GrupoProductoId" FROM "Productos" p WHERE p."Id" = mv."ProductoId"),
                            (SELECT g."Id" FROM "GruposProducto" g WHERE g."Id" = 'f2000000-0000-0000-0000-000000000003'))
                    END,
                    "GrupoNegocioId" = CASE
                        WHEN EXISTS (SELECT 1 FROM "GruposNegocio" g WHERE g."Id" = mv."GrupoNegocioId") THEN mv."GrupoNegocioId"
                        ELSE (
                            SELECT COALESCE(
                                s."GrupoNegocioId",
                                (SELECT g."Id" FROM "GruposNegocio" g WHERE g."Id" = 'f2000000-0000-0000-0000-000000000001'))
                            FROM "MovimientosProducto" mp
                            JOIN "SociosNegocio" s ON s."Id" = mp."SocioNegocioId"
                            WHERE mp."Id" = mv."MovimientoProductoId")
                    END;

                ALTER TABLE "MovimientosValor" ENABLE TRIGGER "TR_MovimientosValor_AppendOnly";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosValor_GruposInventario_GrupoInventarioId",
                table: "MovimientosValor",
                column: "GrupoInventarioId",
                principalTable: "GruposInventario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosValor_GruposNegocio_GrupoNegocioId",
                table: "MovimientosValor",
                column: "GrupoNegocioId",
                principalTable: "GruposNegocio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosValor_GruposProducto_GrupoProductoId",
                table: "MovimientosValor",
                column: "GrupoProductoId",
                principalTable: "GruposProducto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DESTRUCTIVO: borra el libro contable (RegistrosContables y MovimientosContables) pero NO toca
            // MovimientosValor."ImporteCostoPosteadoContabilidad", que sigue marcando como contabilizado lo que el batch de
            // costo ya pasó al libro. Tras un Down+Up el batch NO recontabiliza esos importes (solo ve ImporteCosto <>
            // ImporteCostoPosteadoContabilidad). Para reconstruir el libro hay que poner esa columna a 0 a mano y volver a
            // ejecutar el batch. Hazlo con el trigger append-only de MovimientosValor desactivado: mientras
            // PermitirContabilizacionCosto no esté aplicada, libro_inventario_append_only() rechaza ese UPDATE (con la
            // cadena en HEAD sí lo admite, porque es la única columna actualizable).
            // El backfill de grupos de MovimientosValor no se deshace: los valores quedan (sin FK), igual que antes de Up.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_RegistrosContables_NoTruncate\" ON \"RegistrosContables\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_RegistrosContables_AppendOnly\" ON \"RegistrosContables\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_MovimientosContables_NoTruncate\" ON \"MovimientosContables\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_MovimientosContables_AppendOnly\" ON \"MovimientosContables\";");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosValor_GruposInventario_GrupoInventarioId",
                table: "MovimientosValor");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosValor_GruposNegocio_GrupoNegocioId",
                table: "MovimientosValor");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosValor_GruposProducto_GrupoProductoId",
                table: "MovimientosValor");

            migrationBuilder.DropTable(
                name: "MovimientosContables");

            migrationBuilder.DropTable(
                name: "RegistrosContables");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosValor_GrupoInventarioId",
                table: "MovimientosValor");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosValor_GrupoNegocioId",
                table: "MovimientosValor");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosValor_GrupoProductoId",
                table: "MovimientosValor");

            migrationBuilder.DeleteData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000003"));
        }
    }
}
