using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddIndicesLibroInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MovimientosValor_ProductoId_FechaRegistro",
                table: "MovimientosValor",
                columns: new[] { "ProductoId", "FechaRegistro" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosProducto_TipoDocumento_NumeroDocumento",
                table: "MovimientosProducto",
                columns: new[] { "TipoDocumento", "NumeroDocumento" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosProducto_TipoOrigen_ClaveOrigen",
                table: "MovimientosProducto",
                columns: new[] { "TipoOrigen", "ClaveOrigen" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosValor_ProductoId_FechaRegistro",
                table: "MovimientosValor");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosProducto_TipoDocumento_NumeroDocumento",
                table: "MovimientosProducto");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosProducto_TipoOrigen_ClaveOrigen",
                table: "MovimientosProducto");
        }
    }
}
