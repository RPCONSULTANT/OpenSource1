using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddRegistrosDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosDiario",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    NumeroRegistro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LoteDiarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DesdeMovimientoProducto = table.Column<long>(type: "bigint", nullable: false),
                    HastaMovimientoProducto = table.Column<long>(type: "bigint", nullable: false),
                    Lineas = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosDiario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosDiario_LotesDiario_LoteDiarioId",
                        column: x => x.LoteDiarioId,
                        principalTable: "LotesDiario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosDiario_LoteDiarioId",
                table: "RegistrosDiario",
                column: "LoteDiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosDiario_NumeroRegistro",
                table: "RegistrosDiario",
                column: "NumeroRegistro",
                unique: true);

            // Append-only como el libro de inventario (desviación de la Fase 4): reutiliza la función
            // libro_inventario_append_only() de la migración AddLibroInventario, que rechaza DELETE y todo UPDATE en
            // cualquier tabla distinta de MovimientosProducto; TRUNCATE se bloquea con un trigger de sentencia aparte.
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_RegistrosDiario_AppendOnly" BEFORE UPDATE OR DELETE ON "RegistrosDiario"
                    FOR EACH ROW EXECUTE FUNCTION libro_inventario_append_only();
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER "TR_RegistrosDiario_NoTruncate" BEFORE TRUNCATE ON "RegistrosDiario"
                    FOR EACH STATEMENT EXECUTE FUNCTION libro_inventario_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_RegistrosDiario_NoTruncate\" ON \"RegistrosDiario\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_RegistrosDiario_AppendOnly\" ON \"RegistrosDiario\";");

            migrationBuilder.DropTable(
                name: "RegistrosDiario");
        }
    }
}
