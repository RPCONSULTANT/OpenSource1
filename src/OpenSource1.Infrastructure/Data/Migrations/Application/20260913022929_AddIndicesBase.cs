using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddIndicesBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Productos_CreatedAtUtc",
                table: "Productos",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Entradas_CreatedAtUtc",
                table: "Entradas",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_CreatedAtUtc",
                table: "Clientes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Email",
                table: "Clientes",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_CreatedAtUtc",
                table: "AppSettings",
                column: "CreatedAtUtc");

            // Los ILIKE '%term%' de FilterExpressionBuilder.cs llevan wildcard inicial: ningun
            // indice B-tree sirve para ellos. pg_trgm + GIN si.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_Clientes_Nombre_trgm" ON "Clientes" USING GIN ("Nombre" gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_Clientes_Apellido_trgm" ON "Clientes" USING GIN ("Apellido" gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_Clientes_Email_trgm" ON "Clientes" USING GIN ("Email" gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_Productos_Nombre_trgm" ON "Productos" USING GIN ("Nombre" gin_trgm_ops);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Productos_Nombre_trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Clientes_Email_trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Clientes_Apellido_trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Clientes_Nombre_trgm";""");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CreatedAtUtc",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Entradas_CreatedAtUtc",
                table: "Entradas");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_CreatedAtUtc",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_Email",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_AppSettings_CreatedAtUtc",
                table: "AppSettings");
        }
    }
}
