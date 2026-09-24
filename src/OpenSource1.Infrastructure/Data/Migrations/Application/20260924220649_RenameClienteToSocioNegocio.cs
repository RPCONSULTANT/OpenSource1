using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class RenameClienteToSocioNegocio : Migration
    {
        // NOTA (Task 2.5, corrección respecto al scaffold automático): al cambiar el TIPO de la entidad
        // (Cliente -> SocioDeNegocio) y su tabla, `dotnet ef migrations add` generó
        // DropTable("Clientes") + CreateTable("SociosNegocio"), es decir, DESTRUYE todas las filas
        // existentes. Además el CreateTable incluía la columna "xmin" (columna de sistema de PostgreSQL,
        // mapeada solo como shadow property/rowversion), que PostgreSQL rechaza crear: el mismo defecto
        // que ya obligó a editar a mano AddSoftDelete. Esta migración es un renombre PURO (sin campos
        // nuevos, sin cambio de columnas), así que se reescribió a mano con RenameTable + RenameIndex,
        // que preservan los datos, los Ids y el estado de soft delete.
        //
        // Lo que el scaffold no genera y aquí se hace explícito:
        //  - La clave primaria (PK_Clientes) NO se renombra sola con RenameTable: se renombra con
        //    ALTER TABLE ... RENAME CONSTRAINT (esto renombra también su índice subyacente).
        //  - Los índices trigram GIN (IX_Clientes_{Nombre,Apellido,Email}_trgm) los creó AddIndicesBase con
        //    SQL crudo, EF no los conoce; siguen a la tabla al renombrarla pero conservarían el nombre
        //    "Clientes", así que se renombran también (IF EXISTS: idempotente si alguna base no los tuviera).
        // Down() es simétrico. Verificado contra Postgres real con filas sembradas (incluida una borrada
        // lógicamente) y con una migración de prueba posterior que salió vacía.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Clientes",
                newName: "SociosNegocio");

            migrationBuilder.Sql("""ALTER TABLE "SociosNegocio" RENAME CONSTRAINT "PK_Clientes" TO "PK_SociosNegocio";""");

            migrationBuilder.RenameIndex(
                name: "IX_Clientes_CreatedAtUtc",
                table: "SociosNegocio",
                newName: "IX_SociosNegocio_CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Clientes_Email",
                table: "SociosNegocio",
                newName: "IX_SociosNegocio_Email");

            migrationBuilder.Sql("""ALTER INDEX IF EXISTS "IX_Clientes_Nombre_trgm" RENAME TO "IX_SociosNegocio_Nombre_trgm";""");
            migrationBuilder.Sql("""ALTER INDEX IF EXISTS "IX_Clientes_Apellido_trgm" RENAME TO "IX_SociosNegocio_Apellido_trgm";""");
            migrationBuilder.Sql("""ALTER INDEX IF EXISTS "IX_Clientes_Email_trgm" RENAME TO "IX_SociosNegocio_Email_trgm";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER INDEX IF EXISTS "IX_SociosNegocio_Email_trgm" RENAME TO "IX_Clientes_Email_trgm";""");
            migrationBuilder.Sql("""ALTER INDEX IF EXISTS "IX_SociosNegocio_Apellido_trgm" RENAME TO "IX_Clientes_Apellido_trgm";""");
            migrationBuilder.Sql("""ALTER INDEX IF EXISTS "IX_SociosNegocio_Nombre_trgm" RENAME TO "IX_Clientes_Nombre_trgm";""");

            migrationBuilder.RenameIndex(
                name: "IX_SociosNegocio_Email",
                table: "SociosNegocio",
                newName: "IX_Clientes_Email");

            migrationBuilder.RenameIndex(
                name: "IX_SociosNegocio_CreatedAtUtc",
                table: "SociosNegocio",
                newName: "IX_Clientes_CreatedAtUtc");

            migrationBuilder.Sql("""ALTER TABLE "SociosNegocio" RENAME CONSTRAINT "PK_SociosNegocio" TO "PK_Clientes";""");

            migrationBuilder.RenameTable(
                name: "SociosNegocio",
                newName: "Clientes");
        }
    }
}
