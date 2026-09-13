using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddTerminosPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TerminosPago",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DiasVencimiento = table.Column<int>(type: "integer", nullable: false),
                    DiasDescuento = table.Column<int>(type: "integer", nullable: false),
                    PorcentajeDescuento = table.Column<decimal>(type: "numeric(9,5)", precision: 9, scale: 5, nullable: false),
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
                    table.PrimaryKey("PK_TerminosPago", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TerminosPago_Codigo",
                table: "TerminosPago",
                column: "Codigo",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_TerminosPago_CreatedAtUtc",
                table: "TerminosPago",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TerminosPago");
        }
    }
}
