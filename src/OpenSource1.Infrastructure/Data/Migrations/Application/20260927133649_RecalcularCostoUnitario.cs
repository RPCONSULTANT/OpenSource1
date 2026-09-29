using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <summary>
    /// Migración de datos de la Task 8.3: recalcula la proyección informativa <c>Productos."CostoUnitario"</c> de todos los
    /// productos con movimientos de valor, con la misma definición que desde ahora aplican el registro de movimientos y la
    /// rutina de ajuste (<c>ProyeccionCostoUnitario</c>): <c>V / Q</c> sobre TODOS los movimientos de valor del producto,
    /// redondeado a 4 decimales (half away from zero, como <c>Math.Round(..., AwayFromZero)</c>), solo si <c>Q &gt; 0</c> y el
    /// resultado cabe en <c>[0, 1e14)</c>; en otro caso (y en productos sin movimientos) se conserva el valor.
    /// </summary>
    /// <remarks>
    /// Sin filtro de borrado lógico: un producto borrado con movimientos también se recalcula (su historia sigue contando,
    /// como en la rutina de ajuste, que tampoco filtra <c>IsDeleted</c>).
    /// SQL puro sobre <c>Productos</c>: no lee ni escribe los libros más allá de agregarlos, así que los triggers append-only no
    /// intervienen. Solo actualiza las filas cuyo valor cambia (su <c>xmin</c> cambia: una edición del maestro abierta antes
    /// recibirá 409). El dividendo se lleva a 24 decimales para que la división de PostgreSQL (que por defecto da ~16 cifras
    /// significativas) no altere el redondeo a 4 respecto del cálculo en <c>decimal</c> de .NET.
    /// <para>
    /// <b>Down() no hace nada</b> a propósito: el valor anterior era una proyección desactualizada sin respaldo en ninguna otra
    /// columna, no se puede reconstruir, y conservar el recalculado es inocuo para el esquema anterior.
    /// </para>
    /// </remarks>
    public partial class RecalcularCostoUnitario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Productos" p SET "CostoUnitario" = s."Costo"
                FROM (
                    SELECT "ProductoId",
                           ROUND((SUM("ImporteCosto") + 0.000000000000000000000000) / SUM("CantidadValorada"), 4) AS "Costo"
                    FROM "MovimientosValor"
                    GROUP BY "ProductoId"
                    HAVING SUM("CantidadValorada") > 0
                ) s
                WHERE p."Id" = s."ProductoId"
                  AND s."Costo" >= 0 AND s."Costo" <= 99999999999999.9999
                  AND p."CostoUnitario" <> s."Costo";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op intencionado (ver remarks): la proyección anterior no se puede reconstruir.
        }
    }
}
