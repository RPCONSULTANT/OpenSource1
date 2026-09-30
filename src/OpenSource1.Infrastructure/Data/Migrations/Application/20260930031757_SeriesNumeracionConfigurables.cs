using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class SeriesNumeracionConfigurables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PorDefecto nunca se usó (siempre false): se sustituye por Activa. EF lo propone como RenameColumn (mismo tipo), lo que
            // dejaría todas las series inactivas; se quita y se añade con el valor por defecto true (el SQL de abajo lo ajusta).
            migrationBuilder.DropColumn(
                name: "PorDefecto",
                table: "Series");

            migrationBuilder.AddColumn<bool>(
                name: "Activa",
                table: "Series",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<short>(
                name: "TipoDocumento",
                table: "Series",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "NumeroAviso",
                table: "LineasSerie",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Tipo inferido por código (spec no-series): los códigos fijos de hoy, la familia DIARIO- y las series que usan las
            // plantillas o los lotes de diario. Cualquier otra serie no la numeraba nadie: queda como diario de inventario INACTIVA.
            migrationBuilder.Sql("""
                UPDATE "Series" s SET
                    "TipoDocumento" = CASE s."Codigo"
                        WHEN 'FV-BORR' THEN 1 WHEN 'FV' THEN 2 WHEN 'NC-BORR' THEN 3 WHEN 'NC' THEN 4
                        WHEN 'COBRO' THEN 5 WHEN 'CONTAB' THEN 6 WHEN 'SOCIOS' THEN 7 ELSE 8 END,
                    "Activa" = s."Codigo" IN ('FV-BORR', 'FV', 'NC-BORR', 'NC', 'COBRO', 'CONTAB', 'SOCIOS')
                        OR s."Codigo" LIKE 'DIARIO-%'
                        OR EXISTS (SELECT 1 FROM "PlantillasDiario" p WHERE p."SerieId" = s."Id")
                        OR EXISTS (SELECT 1 FROM "LotesDiario" l WHERE l."SerieId" = s."Id");
                ALTER TABLE "Series" ALTER COLUMN "TipoDocumento" DROP DEFAULT;
                ALTER TABLE "Series" ALTER COLUMN "Activa" DROP DEFAULT;
                -- Contadores sin relleno ("7") al formato completo de su línea ("00000007"): el generador guarda ya el número entero.
                UPDATE "LineasSerie" SET "UltimoNumeroUsado" = lpad("UltimoNumeroUsado", length("NumeroInicial"), '0')
                WHERE "UltimoNumeroUsado" ~ '^[0-9]+$' AND "NumeroInicial" ~ '^[0-9]+$'
                  AND length("UltimoNumeroUsado") < length("NumeroInicial");
                -- Índice único (SerieId, FechaInicial) de más abajo: si hay líneas vivas duplicadas, error claro en vez del 23505.
                DO $$
                DECLARE duplicadas text;
                BEGIN
                    SELECT string_agg(format('%s %s', s."Codigo", l."FechaInicial"), ', ') INTO duplicadas
                    FROM (SELECT "SerieId", "FechaInicial" FROM "LineasSerie" WHERE NOT "IsDeleted"
                          GROUP BY "SerieId", "FechaInicial" HAVING count(*) > 1) l
                    JOIN "Series" s ON s."Id" = l."SerieId";
                    IF duplicadas IS NOT NULL THEN
                        RAISE EXCEPTION 'SeriesNumeracionConfigurables: hay líneas de serie vivas con la misma fecha inicial (%). Borre o cambie la fecha de las sobrantes antes de migrar.', duplicadas;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "ConfiguracionesNumeracion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoDocumento = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_ConfiguracionesNumeracion", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionesNumeracion_TipoDocumento", "\"TipoDocumento\" BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_ConfiguracionesNumeracion_Series_SerieId",
                        column: x => x.SerieId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionesNumeracion",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "SerieId", "TipoDocumento", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("e2000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new Guid("e1000000-0000-0000-0000-000000000005"), (short)1, null, null },
                    { new Guid("e2000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new Guid("e1000000-0000-0000-0000-000000000007"), (short)2, null, null },
                    { new Guid("e2000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new Guid("e1000000-0000-0000-0000-00000000000b"), (short)3, null, null },
                    { new Guid("e2000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new Guid("e1000000-0000-0000-0000-00000000000d"), (short)4, null, null },
                    { new Guid("e2000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new Guid("e1000000-0000-0000-0000-000000000009"), (short)5, null, null },
                    { new Guid("e2000000-0000-0000-0000-000000000006"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new Guid("e1000000-0000-0000-0000-000000000003"), (short)6, null, null },
                    { new Guid("e2000000-0000-0000-0000-000000000007"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new Guid("d1000000-0000-0000-0000-000000000001"), (short)7, null, null },
                    { new Guid("e2000000-0000-0000-0000-000000000008"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new Guid("e1000000-0000-0000-0000-000000000001"), (short)8, null, null }
                });

            migrationBuilder.UpdateData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000002"),
                column: "NumeroAviso",
                value: null);

            migrationBuilder.UpdateData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000004"),
                column: "NumeroAviso",
                value: null);

            migrationBuilder.UpdateData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000006"),
                column: "NumeroAviso",
                value: null);

            migrationBuilder.UpdateData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000008"),
                column: "NumeroAviso",
                value: null);

            migrationBuilder.UpdateData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000a"),
                column: "NumeroAviso",
                value: null);

            migrationBuilder.UpdateData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000c"),
                column: "NumeroAviso",
                value: null);

            migrationBuilder.UpdateData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000e"),
                column: "NumeroAviso",
                value: null);

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000001"),
                columns: new[] { "Activa", "TipoDocumento" },
                values: new object[] { true, (short)8 });

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000003"),
                columns: new[] { "Activa", "TipoDocumento" },
                values: new object[] { true, (short)6 });

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000005"),
                columns: new[] { "Activa", "TipoDocumento" },
                values: new object[] { true, (short)1 });

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000007"),
                columns: new[] { "Activa", "TipoDocumento" },
                values: new object[] { true, (short)2 });

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000009"),
                columns: new[] { "Activa", "TipoDocumento" },
                values: new object[] { true, (short)5 });

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000b"),
                columns: new[] { "Activa", "TipoDocumento" },
                values: new object[] { true, (short)3 });

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000d"),
                columns: new[] { "Activa", "TipoDocumento" },
                values: new object[] { true, (short)4 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Series_TipoDocumento",
                table: "Series",
                sql: "\"TipoDocumento\" BETWEEN 1 AND 8");

            migrationBuilder.CreateIndex(
                name: "IX_LineasSerie_SerieId_FechaInicial",
                table: "LineasSerie",
                columns: new[] { "SerieId", "FechaInicial" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesNumeracion_SerieId",
                table: "ConfiguracionesNumeracion",
                column: "SerieId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesNumeracion_TipoDocumento",
                table: "ConfiguracionesNumeracion",
                column: "TipoDocumento",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesNumeracion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Series_TipoDocumento",
                table: "Series");

            migrationBuilder.DropIndex(
                name: "IX_LineasSerie_SerieId_FechaInicial",
                table: "LineasSerie");

            migrationBuilder.DropColumn(
                name: "TipoDocumento",
                table: "Series");

            migrationBuilder.DropColumn(
                name: "NumeroAviso",
                table: "LineasSerie");

            migrationBuilder.DropColumn(
                name: "Activa",
                table: "Series");

            migrationBuilder.AddColumn<bool>(
                name: "PorDefecto",
                table: "Series",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000001"),
                column: "PorDefecto",
                value: false);

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000003"),
                column: "PorDefecto",
                value: false);

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000005"),
                column: "PorDefecto",
                value: false);

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000007"),
                column: "PorDefecto",
                value: false);

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000009"),
                column: "PorDefecto",
                value: false);

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000b"),
                column: "PorDefecto",
                value: false);

            migrationBuilder.UpdateData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000d"),
                column: "PorDefecto",
                value: false);
        }
    }
}
