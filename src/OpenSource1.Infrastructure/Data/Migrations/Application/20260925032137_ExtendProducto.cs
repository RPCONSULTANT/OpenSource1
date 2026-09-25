using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class ExtendProducto : Migration
    {
        // NOTA (Task 2.9, edición manual del scaffold de `dotnet ef migrations add`).
        //
        // El scaffold generó las operaciones de esquema correctas (columnas nuevas, índices, FK) pero NO sirve tal cual para una tabla
        // con datos, por estos motivos:
        //  1. Borra CategoriaCodigo/CategoriaNombre/UnidadMedidaCodigo/UnidadMedidaNombre y añade CategoriaId/UnidadMedidaBaseId como
        //     NOT NULL con el Guid vacío: PIERDE la categoría y la unidad de cada producto y la FK no se podría crear. Aquí las columnas
        //     nuevas se añaden como NULLABLE, se pueblan con SQL a partir de las columnas legadas, se endurecen a NOT NULL y solo entonces
        //     se borran las legadas.
        //  2. Sustituye Precio por PrecioVenta con DropColumn + AddColumn(defaultValue: 0): PIERDE los precios. Aquí es RenameColumn +
        //     AlterColumn a numeric(18,4) (ensanchar la escala no altera ningún valor).
        //  3. AddColumn NOT NULL con defaultValue deja un DEFAULT permanente en la BD que el modelo no declara (y el scaffold usa
        //     CostoAjustado = false y MetodoCosteo = 0, que no son los valores de negocio). Aquí se añaden con el valor de negocio real
        //     (MetodoCosteo = 1 Promedio, CostoAjustado = true, costos = 0, Bloqueado = 0) y se retira el DEFAULT.
        //
        // El scaffold de esta migración NO incluyó operaciones sobre la columna de sistema `xmin` (el defecto conocido de Npgsql que
        // obligó a editar AddSoftDelete y RenameClienteToSocioNegocio); se revisó explícitamente y no hay nada que quitar.
        //
        // Transformación de datos (Up), con SQL y NO con HasData (así una migración de prueba posterior sale VACÍA):
        //  - Se asegura que existan una categoría GENERAL y una unidad UND ACTIVAS (si alguien las borró lógicamente, se recrean): son el
        //    destino de los productos sin coincidencia.
        //  - Las categorías legadas eran texto libre en Productos: se crea UNA fila de CategoriasProducto por cada código legado distinto
        //    que no exista ya (sin distinguir mayúsculas ni espacios exteriores, con LEFT(...,30) por seguridad de longitud), con
        //    gen_random_uuid(), CreatedAtUtc = now(), CreatedBy = 'system' e IsDeleted = false. Si un mismo código aparece con nombres
        //    distintos gana el del producto más antiguo. Se recorren TODAS las filas, también las borradas lógicamente, para no perder la
        //    clasificación de un producto que se pueda restaurar.
        //  - CategoriaId = categoría ACTIVA cuyo código (normalizado) coincide con el legado; sin coincidencia, GENERAL.
        //  - UnidadMedidaBaseId = unidad ACTIVA cuyo código (normalizado) coincide con el legado; sin coincidencia, UND.
        //  - Stock NO se toca (Ruling L: se mantiene hasta la Fase 3, que lo sustituye por la existencia derivada).
        //
        // Down() es simétrico pero APROXIMADO: recompone CategoriaCodigo/CategoriaNombre y UnidadMedidaCodigo/UnidadMedidaNombre desde el
        // catálogo por Id (también de filas borradas lógicamente), y PrecioVenta vuelve a Precio con escala 2 (los decimales 3 y 4 se
        // redondean). Se pierden MetodoCosteo, CostoUnitario, CostoEstandar, CostoAjustado y Bloqueado. Las categorías creadas por Up NO se
        // borran (son datos válidos del catálogo). Stock no cambia.
        // Verificado contra Postgres real con productos sembrados antes de migrar (categorías distintas incluida una de 30 caracteres con
        // nombre de 100, mayúsculas/espacios variados, unidades variadas, una fila borrada lógicamente y stock distinto de cero), Down + Up
        // de nuevo, y una migración de prueba posterior que salió vacía.

        // Clave de comparación de códigos de categoría: sin distinguir mayúsculas ni espacios exteriores, y con el tope de 30 caracteres de la columna.
        private const string ClaveCodigoProducto = """LEFT(UPPER(TRIM(p."CategoriaCodigo")), 30)""";
        private const string ClaveCodigoCategoria = """LEFT(UPPER(TRIM(c."Codigo")), 30)""";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Precio -> PrecioVenta sin perder datos.
            migrationBuilder.RenameColumn(
                name: "Precio",
                table: "Productos",
                newName: "PrecioVenta");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioVenta",
                table: "Productos",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            // 2. Columnas nuevas: nullable las dos referencias (hasta poblarlas); el resto con su valor de negocio real.
            migrationBuilder.AddColumn<Guid>(
                name: "CategoriaId",
                table: "Productos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnidadMedidaBaseId",
                table: "Productos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "MetodoCosteo",
                table: "Productos",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoUnitario",
                table: "Productos",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostoEstandar",
                table: "Productos",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "CostoAjustado",
                table: "Productos",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<short>(
                name: "Bloqueado",
                table: "Productos",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            // El modelo no declara defaults de BD (los rellena siempre el handler): se retiran los del AddColumn.
            foreach (var columna in new[] { "MetodoCosteo", "CostoUnitario", "CostoEstandar", "CostoAjustado", "Bloqueado" })
            {
                migrationBuilder.Sql($"""ALTER TABLE "Productos" ALTER COLUMN "{columna}" DROP DEFAULT;""");
            }

            // 3. Destinos por defecto: deben existir ACTIVOS (si se borraron lógicamente, se recrean).
            migrationBuilder.Sql("""
                INSERT INTO "CategoriasProducto" ("Id", "Codigo", "Nombre", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                SELECT gen_random_uuid(), 'GENERAL', 'General', now(), 'system', false
                WHERE NOT EXISTS (SELECT 1 FROM "CategoriasProducto" WHERE "IsDeleted" = false AND "Codigo" = 'GENERAL');
                """);

            migrationBuilder.Sql("""
                INSERT INTO "UnidadesMedida" ("Id", "Codigo", "Nombre", "Decimales", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                SELECT gen_random_uuid(), 'UND', 'Unidad', 0, now(), 'system', false
                WHERE NOT EXISTS (SELECT 1 FROM "UnidadesMedida" WHERE "IsDeleted" = false AND "Codigo" = 'UND');
                """);

            // 4a. Una categoría por cada código legado distinto que aún no exista (Ruling O).
            migrationBuilder.Sql($"""
                INSERT INTO "CategoriasProducto" ("Id", "Codigo", "Nombre", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                SELECT gen_random_uuid(), d."Codigo", d."Nombre", now(), 'system', false
                FROM (
                    SELECT DISTINCT ON ({ClaveCodigoProducto})
                           {ClaveCodigoProducto} AS "Codigo",
                           COALESCE(NULLIF(LEFT(TRIM(p."CategoriaNombre"), 100), ''), {ClaveCodigoProducto}) AS "Nombre"
                    FROM "Productos" p
                    WHERE TRIM(p."CategoriaCodigo") <> ''
                    ORDER BY {ClaveCodigoProducto}, p."CreatedAtUtc", p."Id"
                ) d
                WHERE NOT EXISTS (
                    SELECT 1 FROM "CategoriasProducto" c
                    WHERE c."IsDeleted" = false AND {ClaveCodigoCategoria} = d."Codigo");
                """);

            // 4b. CategoriaId por coincidencia de código; sin coincidencia, GENERAL.
            migrationBuilder.Sql($"""
                UPDATE "Productos" AS p
                SET "CategoriaId" = COALESCE(
                    (SELECT c."Id" FROM "CategoriasProducto" c
                     WHERE c."IsDeleted" = false AND {ClaveCodigoCategoria} = {ClaveCodigoProducto}
                     ORDER BY c."CreatedAtUtc", c."Id" LIMIT 1),
                    (SELECT c."Id" FROM "CategoriasProducto" c WHERE c."IsDeleted" = false AND c."Codigo" = 'GENERAL' ORDER BY c."CreatedAtUtc", c."Id" LIMIT 1));
                """);

            // 4c. UnidadMedidaBaseId por coincidencia de código; sin coincidencia, UND.
            migrationBuilder.Sql("""
                UPDATE "Productos" AS p
                SET "UnidadMedidaBaseId" = COALESCE(
                    (SELECT u."Id" FROM "UnidadesMedida" u
                     WHERE u."IsDeleted" = false AND UPPER(TRIM(u."Codigo")) = UPPER(TRIM(p."UnidadMedidaCodigo"))
                     ORDER BY u."CreatedAtUtc", u."Id" LIMIT 1),
                    (SELECT u."Id" FROM "UnidadesMedida" u WHERE u."IsDeleted" = false AND u."Codigo" = 'UND' ORDER BY u."CreatedAtUtc", u."Id" LIMIT 1));
                """);

            // 5. Endurecer a NOT NULL, índices y FK; solo entonces se borran las columnas legadas.
            migrationBuilder.AlterColumn<Guid>(
                name: "CategoriaId",
                table: "Productos",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UnidadMedidaBaseId",
                table: "Productos",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_UnidadMedidaBaseId",
                table: "Productos",
                column: "UnidadMedidaBaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_CategoriasProducto_CategoriaId",
                table: "Productos",
                column: "CategoriaId",
                principalTable: "CategoriasProducto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_UnidadesMedida_UnidadMedidaBaseId",
                table: "Productos",
                column: "UnidadMedidaBaseId",
                principalTable: "UnidadesMedida",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "CategoriaCodigo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CategoriaNombre",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "UnidadMedidaCodigo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "UnidadMedidaNombre",
                table: "Productos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Columnas legadas: nullable hasta recomponerlas desde el catálogo por Id.
            migrationBuilder.AddColumn<string>(
                name: "CategoriaCodigo",
                table: "Productos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoriaNombre",
                table: "Productos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadMedidaCodigo",
                table: "Productos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadMedidaNombre",
                table: "Productos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Productos" AS p
                SET "CategoriaCodigo" = c."Codigo", "CategoriaNombre" = c."Nombre"
                FROM "CategoriasProducto" c
                WHERE c."Id" = p."CategoriaId";
                """);

            migrationBuilder.Sql("""
                UPDATE "Productos" AS p
                SET "UnidadMedidaCodigo" = u."Codigo", "UnidadMedidaNombre" = u."Nombre"
                FROM "UnidadesMedida" u
                WHERE u."Id" = p."UnidadMedidaBaseId";
                """);

            foreach (var (columna, tipo) in new[]
            {
                ("CategoriaCodigo", "character varying(30)"),
                ("CategoriaNombre", "character varying(100)"),
                ("UnidadMedidaCodigo", "character varying(10)"),
                ("UnidadMedidaNombre", "character varying(50)"),
            })
            {
                migrationBuilder.Sql($"""ALTER TABLE "Productos" ALTER COLUMN "{columna}" SET NOT NULL;""");
            }

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_CategoriasProducto_CategoriaId",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_UnidadesMedida_UnidadMedidaBaseId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_CategoriaId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_UnidadMedidaBaseId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Bloqueado",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CostoAjustado",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CostoEstandar",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "CostoUnitario",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "MetodoCosteo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "UnidadMedidaBaseId",
                table: "Productos");

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioVenta",
                table: "Productos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.RenameColumn(
                name: "PrecioVenta",
                table: "Productos",
                newName: "Precio");
        }
    }
}
