using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <summary>
    /// Task 8.5: fechas de registro permitidas. <c>ConfiguracionesRegistro</c> es una fila única sembrada (Id fijo
    /// <c>ConfiguracionRegistroIds.General</c>) SIN límites, así que la migración no restringe ningún posteo existente;
    /// <c>ConfiguracionesRegistroUsuario</c> guarda las excepciones por usuario (<c>UsuarioId</c> referencia lógica a la base de
    /// Identity, único entre las filas vivas).
    /// </summary>
    public partial class AddFechasRegistroPermitidas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionesRegistro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitirRegistroDesde = table.Column<DateOnly>(type: "date", nullable: true),
                    PermitirRegistroHasta = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_ConfiguracionesRegistro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesRegistroUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreUsuario = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PermitirRegistroDesde = table.Column<DateOnly>(type: "date", nullable: true),
                    PermitirRegistroHasta = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_ConfiguracionesRegistroUsuario", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionesRegistro",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "PermitirRegistroDesde", "PermitirRegistroHasta", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("f8000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesRegistroUsuario_UsuarioId",
                table: "ConfiguracionesRegistroUsuario",
                column: "UsuarioId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesRegistro");

            migrationBuilder.DropTable(
                name: "ConfiguracionesRegistroUsuario");
        }
    }
}
