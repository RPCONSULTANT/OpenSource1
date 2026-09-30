using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class BorradoresEnlaceUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotasCreditoVentaBorrador_NotaCreditoVentaNumero",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropIndex(
                name: "IX_FacturasVentaBorrador_FacturaVentaNumero",
                table: "FacturasVentaBorrador");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_NotaCreditoVentaNumero",
                table: "NotasCreditoVentaBorrador",
                column: "NotaCreditoVentaNumero",
                unique: true,
                filter: "\"NotaCreditoVentaNumero\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_FacturaVentaNumero",
                table: "FacturasVentaBorrador",
                column: "FacturaVentaNumero",
                unique: true,
                filter: "\"FacturaVentaNumero\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotasCreditoVentaBorrador_NotaCreditoVentaNumero",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropIndex(
                name: "IX_FacturasVentaBorrador_FacturaVentaNumero",
                table: "FacturasVentaBorrador");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_NotaCreditoVentaNumero",
                table: "NotasCreditoVentaBorrador",
                column: "NotaCreditoVentaNumero");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_FacturaVentaNumero",
                table: "FacturasVentaBorrador",
                column: "FacturaVentaNumero");
        }
    }
}
