using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class RedesPosteoFacturasVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosCliente_TipoDocumento_NumeroDocumento",
                table: "MovimientosCliente");

            migrationBuilder.DropIndex(
                name: "IX_FacturasVenta_RegistroContableId",
                table: "FacturasVenta");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCliente_TipoDocumento_NumeroDocumento",
                table: "MovimientosCliente",
                columns: new[] { "TipoDocumento", "NumeroDocumento" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LineasFacturaVenta_MovimientoProducto",
                table: "LineasFacturaVenta",
                sql: "\"Tipo\" <> 1 OR \"MovimientoProductoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_RegistroContableId",
                table: "FacturasVenta",
                column: "RegistroContableId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturasVenta_Redondeo",
                table: "FacturasVenta",
                sql: "\"ImporteSinIva\" = ROUND(\"ImporteSinIva\", 2) AND \"ImporteIva\" = ROUND(\"ImporteIva\", 2) AND \"ImporteTotal\" = ROUND(\"ImporteTotal\", 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosCliente_TipoDocumento_NumeroDocumento",
                table: "MovimientosCliente");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LineasFacturaVenta_MovimientoProducto",
                table: "LineasFacturaVenta");

            migrationBuilder.DropIndex(
                name: "IX_FacturasVenta_RegistroContableId",
                table: "FacturasVenta");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturasVenta_Redondeo",
                table: "FacturasVenta");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCliente_TipoDocumento_NumeroDocumento",
                table: "MovimientosCliente",
                columns: new[] { "TipoDocumento", "NumeroDocumento" });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVenta_RegistroContableId",
                table: "FacturasVenta",
                column: "RegistroContableId");
        }
    }
}
