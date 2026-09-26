using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddSerieCobro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Series",
                columns: new[] { "Id", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Descripcion", "PermiteHuecos", "PorDefecto", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("e1000000-0000-0000-0000-000000000009"), "COBRO", new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Cobros de clientes", false, false, null, null });

            migrationBuilder.InsertData(
                table: "LineasSerie",
                columns: new[] { "Id", "Bloqueada", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "FechaInicial", "Incremento", "NumeroFinal", "NumeroInicial", "SerieId", "UltimoNumeroUsado", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("e1000000-0000-0000-0000-00000000000a"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, new DateOnly(2020, 1, 1), 1, "99999999", "00000001", new Guid("e1000000-0000-0000-0000-000000000009"), "00000000", null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LineasSerie",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "Series",
                keyColumn: "Id",
                keyValue: new Guid("e1000000-0000-0000-0000-000000000009"));
        }
    }
}
