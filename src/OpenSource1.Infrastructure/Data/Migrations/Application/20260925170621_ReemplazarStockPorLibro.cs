using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    /// <remarks>
    /// Task 3.6: elimina la columna legada <c>Productos.Stock</c> y migra su valor a movimientos de APERTURA en el libro de
    /// inventario (almacén <c>PRINCIPAL</c>, sembrado en la Task 3.2), con <c>TipoOrigen = 99</c> (Migracion) y
    /// <c>ClaveOrigen = 'MIGRACION-STOCK'</c>. A partir de aquí la existencia de un producto se deriva sumando
    /// <c>MovimientosProducto.Cantidad</c> (ver <c>IConsultaInventario</c>); no vuelve a guardarse en el maestro.
    /// <para>
    /// Un Stock positivo se migra como una ENTRADA (TipoMovimiento = AjustePositivo = 3) con <c>CantidadRestante</c> igual a la
    /// cantidad completa (nada se ha aplicado todavía por FIFO). Un Stock negativo se migra como una SALIDA
    /// (TipoMovimiento = AjusteNegativo = 4); por construcción es el PRIMER movimiento del producto (el libro está vacío antes
    /// de migrar) y nace SIN aplicaciones en <c>AplicacionesMovimientoProducto</c>: mientras no exista una entrada posterior que
    /// la cubra, <c>SUM("CantidadRestante")</c> de ese almacén NO coincide con la existencia derivada (que sí es correcta, porque
    /// se calcula sumando "Cantidad", no "CantidadRestante"). Esto es seguro porque el servicio de registro valida disponibilidad
    /// con <c>min(existencia, CantidadRestante aplicable)</c>, nunca solo con "CantidadRestante". La rutina de ajuste de costo
    /// (Task 3.5) valorará esa salida en cuanto se registre la siguiente entrada del producto.
    /// </para>
    /// <para>
    /// Un Stock = 0 no genera movimiento (no hay nada que abrir). Los productos borrados lógicamente SÍ se migran: su historia
    /// de existencia se conserva aunque el producto ya no sea visible.
    /// </para>
    /// <para>
    /// Ruling AR (controlador): tras migrar, los productos cuyo Stock legado era NEGATIVO quedan con <c>CostoAjustado = false</c>
    /// (la salida de apertura queda pendiente de que la rutina de ajuste la valore con la siguiente entrada). Los de Stock
    /// positivo CONSERVAN su <c>CostoAjustado</c> (ya nacía en <c>true</c>: una entrada de apertura sin salidas previas no deja
    /// nada pendiente de ajustar).
    /// </para>
    /// <para>
    /// <c>FechaRegistro</c>/<c>FechaDocumento</c> = fecha de la migración (<c>CURRENT_DATE</c>): es una aproximación aceptada
    /// (no se conoce la fecha real de cada Stock legado).
    /// </para>
    /// <para>
    /// <c>Down()</c> reconstruye <c>Stock</c> sumando SOLO los movimientos con <c>TipoOrigen = 99</c> (los que insertó esta
    /// misma migración), redondeados a entero: si tras aplicar <c>Up()</c> se registraron movimientos adicionales (diarios,
    /// ajustes de costo, etc.), <c>Down()</c> los PIERDE — no es un rollback completo del libro, solo de esta migración.
    /// </para>
    /// </remarks>
    public partial class ReemplazarStockPorLibro : Migration
    {
        private const string AlmacenPrincipalId = "b1000000-0000-0000-0000-000000000001";
        private const string ClaveOrigenMigracion = "MIGRACION-STOCK";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Por si el almacén PRINCIPAL hubiera sido borrado lógicamente antes de migrar (no debería: es el único
            // predeterminado y "Almacenes" bloquea borrar el predeterminado), se reactiva para poder referenciarlo.
            migrationBuilder.Sql($"""
                UPDATE "Almacenes" SET "IsDeleted" = false, "DeletedAtUtc" = NULL, "DeletedBy" = NULL
                WHERE "Id" = '{AlmacenPrincipalId}' AND "IsDeleted" = true;
                """);

            // Apertura: un movimiento por producto con Stock <> 0 (incluye productos borrados lógicamente: su historia se
            // conserva). TipoMovimiento 3 = AjustePositivo (Stock > 0, entrada) / 4 = AjusteNegativo (Stock < 0, salida).
            // TipoDocumento 0 = Ninguno. CantidadRestante solo tiene sentido en una entrada (nace igual a la cantidad
            // completa, nada aplicado todavía); en una salida es NULL. El "NOT EXISTS" hace la migración idempotente: una
            // reejecución (o un Down()+Up() en pruebas) no vuelve a abrir el mismo producto ni duplica su existencia.
            migrationBuilder.Sql($"""
                INSERT INTO "MovimientosProducto" ("ProductoId","AlmacenId","TipoMovimiento","TipoDocumento","NumeroDocumento",
                    "NumeroLineaDocumento","FechaRegistro","FechaDocumento","Cantidad","CantidadRestante","CantidadFacturada",
                    "UnidadMedidaId","CantidadPorUnidadMedida","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
                SELECT p."Id", '{AlmacenPrincipalId}',
                       CASE WHEN p."Stock" > 0 THEN 3 ELSE 4 END, 0, NULL, 0,
                       CURRENT_DATE, CURRENT_DATE, p."Stock",
                       CASE WHEN p."Stock" > 0 THEN p."Stock" END, 0,
                       p."UnidadMedidaBaseId", 1, 99, '{ClaveOrigenMigracion}', now(), 'migracion'
                FROM "Productos" p
                WHERE p."Stock" <> 0
                  AND NOT EXISTS (
                      SELECT 1 FROM "MovimientosProducto" mp
                      WHERE mp."ProductoId" = p."Id" AND mp."ClaveOrigen" = '{ClaveOrigenMigracion}'
                  );
                """);

            // Valor: costo = Stock (con signo) x CostoUnitario vigente del producto en ese momento. TipoValor 1 =
            // CostoDirecto. Se enlaza por ClaveOrigen (único de esta migración) y ProductoId, no por rango de Id, para que la
            // migración sea segura de reejecutar en un entorno de pruebas que la aplique más de una vez sin duplicar otras filas.
            migrationBuilder.Sql($"""
                INSERT INTO "MovimientosValor" ("MovimientoProductoId","ProductoId","AlmacenId","TipoValor","TipoMovimiento",
                    "FechaRegistro","CantidadValorada","CantidadFacturada","ImporteCosto","CostoPorUnidad","ImporteVenta",
                    "ImporteCostoPosteadoContabilidad","Ajuste","TipoDocumento","NumeroDocumento","NumeroLineaDocumento",
                    "TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
                SELECT m."Id", m."ProductoId", m."AlmacenId", 1, m."TipoMovimiento", m."FechaRegistro", m."Cantidad", 0,
                       ROUND(m."Cantidad" * p."CostoUnitario", 4), p."CostoUnitario", 0, 0, false, 0, NULL, 0,
                       99, '{ClaveOrigenMigracion}', now(), 'migracion'
                FROM "MovimientosProducto" m JOIN "Productos" p ON p."Id" = m."ProductoId"
                WHERE m."ClaveOrigen" = '{ClaveOrigenMigracion}';
                """);

            // Ruling AR: la salida de apertura (Stock legado negativo) queda pendiente de ajuste de costo hasta que la
            // rutina (Task 3.5) la valore con la siguiente entrada; si el producto quedara "ajustado" aquí, la pasada
            // global no lo recorrería nunca. Los de Stock positivo conservan su CostoAjustado (ya nacía en true).
            migrationBuilder.Sql("""UPDATE "Productos" SET "CostoAjustado" = false WHERE "Stock" < 0;""");

            migrationBuilder.DropColumn(
                name: "Stock",
                table: "Productos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Stock",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Reconstruye Stock SOLO a partir de los movimientos que insertó esta migración (TipoOrigen = 99): cualquier
            // movimiento posterior (diarios, ajustes de costo) se pierde en este Down, documentado más arriba.
            migrationBuilder.Sql("""
                UPDATE "Productos" p SET "Stock" = COALESCE((
                    SELECT ROUND(SUM(m."Cantidad"))
                    FROM "MovimientosProducto" m
                    WHERE m."ProductoId" = p."Id" AND m."TipoOrigen" = 99
                ), 0)::integer;
                """);
        }
    }
}
