using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class ExtendSocioNegocio : Migration
    {
        // NOTA (Task 2.6, edición manual del scaffold de `dotnet ef migrations add`).
        //
        // El scaffold generó las operaciones de esquema correctas (columnas nuevas, índices, FK) pero NO
        // sirve tal cual para una tabla con datos, por tres motivos:
        //  1. Sustituye Nombre/Apellido por NombreComercial con DropColumn + AddColumn(defaultValue: ""),
        //     es decir, PIERDE los nombres. Aquí se añaden las columnas como NULLABLE, se puebla con un
        //     UPDATE, se endurecen a NOT NULL y solo entonces se borran Nombre/Apellido.
        //  2. AddColumn NOT NULL con defaultValue deja un DEFAULT permanente en la BD para Codigo/
        //     NombreComercial/Tipo/TipoDocumentoFiscal que el modelo no declara (y con Codigo = '' para
        //     todas las filas, el índice único parcial no se podría crear).
        //  3. No conoce lo que se crea con SQL crudo: al borrar Nombre/Apellido, Postgres elimina EN
        //     SILENCIO sus índices trigram GIN (IX_SociosNegocio_Nombre_trgm/_Apellido_trgm, creados con
        //     SQL crudo en AddIndicesBase) y la búsqueda por nombre quedaría sin índice. Se crea
        //     IX_SociosNegocio_NombreComercial_trgm y Down() recrea los dos originales.
        //
        // El scaffold de esta migración NO incluyó operaciones sobre la columna de sistema `xmin`
        // (el defecto conocido de Npgsql que obligó a editar AddSoftDelete y RenameClienteToSocioNegocio);
        // se revisó explícitamente y no hay nada que quitar.
        //
        // Transformación de datos (Up):
        //  - NombreComercial = LEFT(TRIM(Nombre || ' ' || Apellido), 200). Nombre (100) + espacio +
        //    Apellido (100) puede dar 201 caracteres y NombreComercial es varchar(200): sin el LEFT el
        //    UPDATE fallaría con datos límite.
        //  - Tipo = 1 (Cliente), TipoDocumentoFiscal = 9 (SinDocumento).
        //  - Codigo = LPAD(ROW_NUMBER() OVER (ORDER BY CreatedAtUtc, Id), 6, '0') sobre TODAS las filas,
        //    incluidas las borradas lógicamente (así el código nunca se reutiliza ni colisiona).
        //    LPAD trunca si el número tiene más de 6 dígitos, por eso se aborta antes si hay más de
        //    999999 filas.
        //  - La serie SOCIOS (Series + LineasSerie) se siembra con SQL y NO con HasData porque el
        //    UltimoNumeroUsado depende de los datos: es el total de filas migradas, de modo que el
        //    primer alta nueva recibe el código SIGUIENTE. Al no usar HasData, una migración de prueba
        //    posterior sale vacía. Ids fijos para que sea determinista y Down() pueda deshacerla.
        //
        // Down() es simétrico pero APROXIMADO: Nombre = primera palabra de NombreComercial y Apellido =
        // el resto ('-' si no hay resto, porque Apellido era NOT NULL); Email nulo pasa a ''; se pierden
        // Codigo, Tipo, documento fiscal, término de pago, límite de crédito y bloqueo, y la serie SOCIOS.
        // Verificado contra Postgres real con filas sembradas (completa, con nulos, borrada lógicamente y
        // con Nombre/Apellido de 100 caracteres cada uno), Down + Up de nuevo, y una migración de prueba
        // posterior que salió vacía.

        private const string SerieSociosId = "d1000000-0000-0000-0000-000000000001";
        private const string LineaSociosId = "d1000000-0000-0000-0000-000000000002";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Columnas nuevas: nullable (o con default de negocio real) hasta poblarlas.
            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "SociosNegocio",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Tipo",
                table: "SociosNegocio",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreComercial",
                table: "SociosNegocio",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "SociosNegocio",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "TipoDocumentoFiscal",
                table: "SociosNegocio",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDocumentoFiscal",
                table: "SociosNegocio",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ciudad",
                table: "SociosNegocio",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminoPagoId",
                table: "SociosNegocio",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LimiteCredito",
                table: "SociosNegocio",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<short>(
                name: "Bloqueado",
                table: "SociosNegocio",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SociosNegocio",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            // 2. Transformación de datos.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF (SELECT COUNT(*) FROM "SociosNegocio") > 999999 THEN
                        RAISE EXCEPTION 'SociosNegocio tiene más de 999999 filas: el código de 6 dígitos no alcanza.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE "SociosNegocio" AS s
                SET "NombreComercial" = LEFT(TRIM(s."Nombre" || ' ' || s."Apellido"), 200),
                    "Tipo" = 1,
                    "TipoDocumentoFiscal" = 9,
                    "Codigo" = n."Codigo"
                FROM (
                    SELECT "Id", LPAD(ROW_NUMBER() OVER (ORDER BY "CreatedAtUtc", "Id")::text, 6, '0') AS "Codigo"
                    FROM "SociosNegocio"
                ) AS n
                WHERE n."Id" = s."Id";
                """);

            // 3. Endurecer a NOT NULL (sin DEFAULT residual).
            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                table: "SociosNegocio",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "Tipo",
                table: "SociosNegocio",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NombreComercial",
                table: "SociosNegocio",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "TipoDocumentoFiscal",
                table: "SociosNegocio",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            // 4. Borrar Nombre/Apellido (Postgres elimina también sus índices trigram GIN).
            migrationBuilder.DropColumn(
                name: "Apellido",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "Nombre",
                table: "SociosNegocio");

            // 5. Índices y FK.
            migrationBuilder.CreateIndex(
                name: "IX_SociosNegocio_Codigo",
                table: "SociosNegocio",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_SociosNegocio_NumeroDocumentoFiscal",
                table: "SociosNegocio",
                column: "NumeroDocumentoFiscal",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"NumeroDocumentoFiscal\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SociosNegocio_TerminoPagoId",
                table: "SociosNegocio",
                column: "TerminoPagoId");

            migrationBuilder.AddForeignKey(
                name: "FK_SociosNegocio_TerminosPago_TerminoPagoId",
                table: "SociosNegocio",
                column: "TerminoPagoId",
                principalTable: "TerminosPago",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Índice trigram (SQL crudo: EF no lo conoce) que sustituye a los de Nombre/Apellido.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_SociosNegocio_NombreComercial_trgm" ON "SociosNegocio" USING GIN ("NombreComercial" gin_trgm_ops);""");

            // 6. Serie SOCIOS con el contador ya posicionado tras las filas migradas.
            migrationBuilder.Sql($"""
                INSERT INTO "Series" ("Id", "Codigo", "Descripcion", "PermiteHuecos", "PorDefecto", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES ('{SerieSociosId}', 'SOCIOS', 'Códigos de socios de negocio', false, false, now(), 'system', false);
                """);

            migrationBuilder.Sql($"""
                INSERT INTO "LineasSerie" ("Id", "SerieId", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "FechaInicial", "Incremento", "Bloqueada", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES ('{LineaSociosId}', '{SerieSociosId}', '000001', '999999',
                        LPAD((SELECT COUNT(*) FROM "SociosNegocio")::text, 6, '0'),
                        DATE '2000-01-01', 1, false, now(), 'system', false);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""DELETE FROM "LineasSerie" WHERE "Id" = '{LineaSociosId}';""");
            migrationBuilder.Sql($"""DELETE FROM "Series" WHERE "Id" = '{SerieSociosId}';""");

            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_SociosNegocio_NombreComercial_trgm";""");

            migrationBuilder.DropForeignKey(
                name: "FK_SociosNegocio_TerminosPago_TerminoPagoId",
                table: "SociosNegocio");

            migrationBuilder.DropIndex(
                name: "IX_SociosNegocio_Codigo",
                table: "SociosNegocio");

            migrationBuilder.DropIndex(
                name: "IX_SociosNegocio_NumeroDocumentoFiscal",
                table: "SociosNegocio");

            migrationBuilder.DropIndex(
                name: "IX_SociosNegocio_TerminoPagoId",
                table: "SociosNegocio");

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "SociosNegocio",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Apellido",
                table: "SociosNegocio",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Recomposición aproximada: primera palabra -> Nombre, resto -> Apellido ('-' si no hay).
            migrationBuilder.Sql("""
                UPDATE "SociosNegocio"
                SET "Nombre" = LEFT(SPLIT_PART("NombreComercial", ' ', 1), 100),
                    "Apellido" = LEFT(
                        CASE WHEN POSITION(' ' IN "NombreComercial") = 0 THEN '-'
                             ELSE COALESCE(NULLIF(TRIM(SUBSTRING("NombreComercial" FROM POSITION(' ' IN "NombreComercial") + 1)), ''), '-')
                        END, 100);
                """);

            migrationBuilder.Sql("""UPDATE "SociosNegocio" SET "Email" = '' WHERE "Email" IS NULL;""");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "SociosNegocio",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Apellido",
                table: "SociosNegocio",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SociosNegocio",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            // Los índices trigram de Nombre/Apellido los creó AddIndicesBase con SQL crudo (con el
            // nombre de tabla renombrado por RenameClienteToSocioNegocio).
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_SociosNegocio_Nombre_trgm" ON "SociosNegocio" USING GIN ("Nombre" gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_SociosNegocio_Apellido_trgm" ON "SociosNegocio" USING GIN ("Apellido" gin_trgm_ops);""");

            migrationBuilder.DropColumn(
                name: "Bloqueado",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "Ciudad",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "LimiteCredito",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "NombreComercial",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "NumeroDocumentoFiscal",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "TerminoPagoId",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "SociosNegocio");

            migrationBuilder.DropColumn(
                name: "TipoDocumentoFiscal",
                table: "SociosNegocio");
        }
    }
}
