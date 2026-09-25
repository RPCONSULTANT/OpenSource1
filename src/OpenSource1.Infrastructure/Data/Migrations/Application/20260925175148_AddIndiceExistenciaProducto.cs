using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    /// <remarks>
    /// Task 3.6, corrección de rendimiento (ronda 1): índice NO parcial <c>("ProductoId","AlmacenId")</c> con
    /// <c>INCLUDE ("Cantidad","FechaRegistro")</c> sobre <c>MovimientosProducto</c>. El único índice existente que arranca
    /// en <c>ProductoId</c> era el parcial de FIFO (<c>IX_MovimientosProducto_Fifo</c>, filtro
    /// <c>"CantidadRestante" &gt; 0</c>), que NO cubre las filas de salida (siempre <c>CantidadRestante IS NULL</c>) ni
    /// permite un Index Only Scan para <c>SUM("Cantidad")</c>: el listado, <c>GetByIdAsync</c> y
    /// <c>/existencias</c> recorrían la tabla completa. Con este índice, Postgres resuelve la agregación de existencia con
    /// un Index Only Scan sin volver al heap (ver el EXPLAIN ANALYZE del informe de la Task 3.6).
    /// </remarks>
    public partial class AddIndiceExistenciaProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MovimientosProducto_Existencia",
                table: "MovimientosProducto",
                columns: new[] { "ProductoId", "AlmacenId" })
                .Annotation("Npgsql:IndexInclude", new[] { "Cantidad", "FechaRegistro" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosProducto_Existencia",
                table: "MovimientosProducto");
        }
    }
}
