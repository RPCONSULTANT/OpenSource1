using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddAlmacenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Almacenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DireccionLinea1 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DireccionLinea2 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Ciudad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaisCodigo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Bloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    EsPredeterminado = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Almacenes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Almacenes",
                columns: new[] { "Id", "Bloqueado", "Ciudad", "Codigo", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "DireccionLinea1", "DireccionLinea2", "EsPredeterminado", "Nombre", "PaisCodigo", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { new Guid("b1000000-0000-0000-0000-000000000001"), false, null, "PRINCIPAL", new DateTimeOffset(new DateTime(2026, 9, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, null, null, true, "Almacén principal", null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Almacenes_Codigo",
                table: "Almacenes",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Almacenes_CreatedAtUtc",
                table: "Almacenes",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Almacenes_EsPredeterminado",
                table: "Almacenes",
                column: "EsPredeterminado",
                unique: true,
                filter: "\"EsPredeterminado\" = true AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Almacenes");
        }
    }
}
