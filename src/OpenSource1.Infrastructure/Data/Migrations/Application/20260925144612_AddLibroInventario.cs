using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddLibroInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovimientosProducto",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoMovimiento = table.Column<short>(type: "smallint", nullable: false),
                    TipoDocumento = table.Column<short>(type: "smallint", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NumeroLineaDocumento = table.Column<int>(type: "integer", nullable: false),
                    FechaRegistro = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CantidadRestante = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CantidadFacturada = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnidadMedidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CantidadPorUnidadMedida = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    SocioNegocioId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoOrigen = table.Column<short>(type: "smallint", nullable: false),
                    ClaveOrigen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosProducto", x => x.Id);
                    table.CheckConstraint("CK_MovimientosProducto_Cantidad_NoCero", "\"Cantidad\" <> 0");
                    table.CheckConstraint("CK_MovimientosProducto_Factor_Positivo", "\"CantidadPorUnidadMedida\" > 0");
                    table.CheckConstraint("CK_MovimientosProducto_Restante", "\"CantidadRestante\" IS NULL OR (\"CantidadRestante\" >= 0 AND \"CantidadRestante\" <= \"Cantidad\")");
                    table.ForeignKey(
                        name: "FK_MovimientosProducto_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosProducto_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosProducto_SociosNegocio_SocioNegocioId",
                        column: x => x.SocioNegocioId,
                        principalTable: "SociosNegocio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosProducto_UnidadesMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalTable: "UnidadesMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AplicacionesMovimientoProducto",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    MovimientoEntradaId = table.Column<long>(type: "bigint", nullable: false),
                    MovimientoSalidaId = table.Column<long>(type: "bigint", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    FechaRegistro = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AplicacionesMovimientoProducto", x => x.Id);
                    table.CheckConstraint("CK_Aplicaciones_Cantidad_Positiva", "\"Cantidad\" > 0");
                    table.ForeignKey(
                        name: "FK_AplicacionesMovimientoProducto_MovimientosProducto_Movimien~",
                        column: x => x.MovimientoEntradaId,
                        principalTable: "MovimientosProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AplicacionesMovimientoProducto_MovimientosProducto_Movimie~1",
                        column: x => x.MovimientoSalidaId,
                        principalTable: "MovimientosProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosValor",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    MovimientoProductoId = table.Column<long>(type: "bigint", nullable: true),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoValor = table.Column<short>(type: "smallint", nullable: false),
                    TipoMovimiento = table.Column<short>(type: "smallint", nullable: false),
                    FechaRegistro = table.Column<DateOnly>(type: "date", nullable: false),
                    CantidadValorada = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CantidadFacturada = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ImporteCosto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CostoPorUnidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImporteVenta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ImporteCostoPosteadoContabilidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    Ajuste = table.Column<bool>(type: "boolean", nullable: false),
                    TipoDocumento = table.Column<short>(type: "smallint", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NumeroLineaDocumento = table.Column<int>(type: "integer", nullable: false),
                    GrupoInventarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoNegocioId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrupoProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoOrigen = table.Column<short>(type: "smallint", nullable: false),
                    ClaveOrigen = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosValor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosValor_Almacenes_AlmacenId",
                        column: x => x.AlmacenId,
                        principalTable: "Almacenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosValor_MovimientosProducto_MovimientoProductoId",
                        column: x => x.MovimientoProductoId,
                        principalTable: "MovimientosProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosValor_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AplicacionesMovimientoProducto_MovimientoEntradaId",
                table: "AplicacionesMovimientoProducto",
                column: "MovimientoEntradaId");

            migrationBuilder.CreateIndex(
                name: "IX_AplicacionesMovimientoProducto_MovimientoSalidaId",
                table: "AplicacionesMovimientoProducto",
                column: "MovimientoSalidaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosProducto_AlmacenId",
                table: "MovimientosProducto",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosProducto_Fifo",
                table: "MovimientosProducto",
                columns: new[] { "ProductoId", "AlmacenId", "FechaRegistro", "Id" },
                filter: "\"CantidadRestante\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosProducto_SocioNegocioId",
                table: "MovimientosProducto",
                column: "SocioNegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosProducto_UnidadMedidaId",
                table: "MovimientosProducto",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosValor_AlmacenId",
                table: "MovimientosValor",
                column: "AlmacenId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosValor_MovimientoProductoId",
                table: "MovimientosValor",
                column: "MovimientoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosValor_PendientePosteoContabilidad",
                table: "MovimientosValor",
                columns: new[] { "ProductoId", "AlmacenId", "FechaRegistro" },
                filter: "\"ImporteCosto\" <> \"ImporteCostoPosteadoContabilidad\"");

            // Libro append-only: ni el ORM ni ningún cliente SQL directo puede corregir una fila ya
            // escrita, solo insertar una nueva (o, en MovimientosProducto, reducir CantidadRestante al
            // consumirla por FIFO). La función se comparte entre las tres tablas; el trigger de fila
            // decide qué UPDATE tolerar y el de sentencia bloquea TRUNCATE (que no dispara triggers de fila).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION libro_inventario_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'El libro % es de solo inserción: DELETE no permitido', TG_TABLE_NAME USING ERRCODE = 'P0001';
                    END IF;
                    IF TG_TABLE_NAME = 'MovimientosProducto'
                       AND (to_jsonb(NEW) - 'CantidadRestante') = (to_jsonb(OLD) - 'CantidadRestante') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'El libro % es de solo inserción: UPDATE no permitido', TG_TABLE_NAME USING ERRCODE = 'P0001';
                END $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_MovimientosProducto_AppendOnly" BEFORE UPDATE OR DELETE ON "MovimientosProducto"
                    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_MovimientosValor_AppendOnly" BEFORE UPDATE OR DELETE ON "MovimientosValor"
                    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_AplicacionesMovimientoProducto_AppendOnly" BEFORE UPDATE OR DELETE ON "AplicacionesMovimientoProducto"
                    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
                """);

            // TRUNCATE no dispara triggers de fila: se bloquea aparte, con un trigger de sentencia.
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_MovimientosProducto_NoTruncate" BEFORE TRUNCATE ON "MovimientosProducto"
                    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_MovimientosValor_NoTruncate" BEFORE TRUNCATE ON "MovimientosValor"
                    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_AplicacionesMovimientoProducto_NoTruncate" BEFORE TRUNCATE ON "AplicacionesMovimientoProducto"
                    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_AplicacionesMovimientoProducto_NoTruncate\" ON \"AplicacionesMovimientoProducto\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_MovimientosValor_NoTruncate\" ON \"MovimientosValor\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_MovimientosProducto_NoTruncate\" ON \"MovimientosProducto\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_AplicacionesMovimientoProducto_AppendOnly\" ON \"AplicacionesMovimientoProducto\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_MovimientosValor_AppendOnly\" ON \"MovimientosValor\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_MovimientosProducto_AppendOnly\" ON \"MovimientosProducto\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS libro_inventario_append_only();");

            migrationBuilder.DropTable(
                name: "AplicacionesMovimientoProducto");

            migrationBuilder.DropTable(
                name: "MovimientosValor");

            migrationBuilder.DropTable(
                name: "MovimientosProducto");
        }
    }
}
