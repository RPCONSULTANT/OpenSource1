using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddPlanCuentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuentasContables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TipoCuenta = table.Column<short>(type: "smallint", nullable: false),
                    TipoResultado = table.Column<short>(type: "smallint", nullable: false),
                    PosteoDirecto = table.Column<bool>(type: "boolean", nullable: false),
                    Bloqueada = table.Column<bool>(type: "boolean", nullable: false),
                    Sangria = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CuentasContables", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CuentasContables",
                columns: new[] { "Id", "Bloqueada", "CreatedAtUtc", "CreatedBy", "DeletedAtUtc", "DeletedBy", "Nombre", "Numero", "PosteoDirecto", "Sangria", "TipoCuenta", "TipoResultado", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("f1000000-0000-0000-0000-000000000001"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Activos", "1", false, 0, (short)2, (short)2, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000002"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Caja", "1101", true, 1, (short)1, (short)2, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000003"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Cuentas por cobrar clientes", "1201", false, 1, (short)1, (short)2, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000004"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Inventario de mercancías", "1301", false, 1, (short)1, (short)2, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000005"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Pasivos", "2", false, 0, (short)2, (short)2, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000006"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "ITBIS por pagar", "2101", false, 1, (short)1, (short)2, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000007"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Ingresos", "4", false, 0, (short)2, (short)1, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000008"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Ventas", "4101", true, 1, (short)1, (short)1, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000009"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Descuentos sobre ventas", "4102", true, 1, (short)1, (short)1, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000010"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Costos", "5", false, 0, (short)2, (short)1, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000011"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Costo de ventas", "5101", false, 1, (short)1, (short)1, null, null },
                    { new Guid("f1000000-0000-0000-0000-000000000012"), false, new DateTimeOffset(new DateTime(2026, 9, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, null, "Ajustes de inventario", "5201", true, 1, (short)1, (short)1, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuentasContables_CreatedAtUtc",
                table: "CuentasContables",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasContables_Numero",
                table: "CuentasContables",
                column: "Numero",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuentasContables");
        }
    }
}
