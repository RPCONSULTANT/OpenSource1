using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class EnlazarBorradoresPosteadosHeredados : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Antes de la rama no-series el posteo (PostearFacturaVenta / PostearNotaCreditoVenta) borraba lógicamente el borrador y sus
        /// líneas en la misma transacción: primero creaba el documento con <c>CreatedAtUtc = T</c>; después borraba TODAS las líneas vivas
        /// (Dapper, <c>DeletedAtUtc = T' &gt;= T</c>, <c>DeletedBy</c> = creador del documento) y al confirmar borraba la cabecera (UnitOfWork,
        /// <c>DeletedAtUtc = T'' &gt;= T'</c>, que también fija <c>UpdatedAtUtc/UpdatedBy</c>). El Down de <c>BorradoresConSeriesYPosteada</c>
        /// hace lo mismo con <c>now()</c> y <c>'migracion'</c> (cabecera y líneas con la misma hora, posterior al documento).
        /// <para>
        /// Borrador heredado: borrado, sin enlace, con un único documento cuyo <c>NumeroBorrador</c> es su número (mismo socio y, en la
        /// nota, misma factura), creado no después del borrado del borrador; ningún otro borrador (vivo o borrado) comparte su número y
        /// ningún borrador vivo enlaza ya ese documento (índices únicos parciales de <c>Numero</c> y del enlace).
        /// </para>
        /// <para>
        /// Criterio de líneas: solo las borradas en la ventana [<c>CreatedAtUtc</c> del documento, <c>DeletedAtUtc</c> del borrador]. Las
        /// líneas borradas a mano lo fueron antes del posteo (el posteo las bloquea FOR UPDATE y después el borrador ya no está vivo), así
        /// que quedan fuera y siguen borradas. Las restauradas estuvieron vivas a la vez, así que no chocan en los índices únicos parciales.
        /// </para>
        /// <para>
        /// Marca para el Down: <c>UpdatedAtUtc</c> guarda el <c>DeletedAtUtc</c> original y <c>UpdatedBy</c> = <c>'migracion:enlace-borrador'</c>
        /// (líneas) o <c>'migracion:enlace-borrador:&lt;Estado original&gt;'</c> (cabeceras). Un borrador Posteada y sus líneas son de solo
        /// lectura, así que la marca no cambia después.
        /// </para>
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH candidatos AS (
                    SELECT b."Id", f."Numero" AS "Documento", f."CreatedAtUtc" AS "PosteadoEn", b."DeletedAtUtc" AS "BorradoEn"
                    FROM "FacturasVentaBorrador" b
                    JOIN "FacturasVenta" f ON f."NumeroBorrador" = b."Numero" AND f."SocioNegocioId" = b."SocioNegocioId"
                    WHERE b."IsDeleted" = true AND b."FacturaVentaNumero" IS NULL AND b."DeletedAtUtc" >= f."CreatedAtUtc"
                      AND NOT EXISTS (SELECT 1 FROM "FacturasVentaBorrador" o WHERE o."Numero" = b."Numero" AND o."Id" <> b."Id")
                      AND NOT EXISTS (SELECT 1 FROM "FacturasVentaBorrador" o WHERE o."FacturaVentaNumero" = f."Numero" AND o."IsDeleted" = false)
                ),
                lineas AS (
                    UPDATE "LineasFacturaVentaBorrador" l
                    SET "IsDeleted" = false, "UpdatedAtUtc" = l."DeletedAtUtc", "UpdatedBy" = 'migracion:enlace-borrador',
                        "DeletedAtUtc" = NULL, "DeletedBy" = NULL
                    FROM candidatos c
                    WHERE l."FacturaVentaBorradorId" = c."Id" AND l."IsDeleted" = true
                      AND l."DeletedAtUtc" >= c."PosteadoEn" AND l."DeletedAtUtc" <= c."BorradoEn"
                    RETURNING l."Id"
                )
                UPDATE "FacturasVentaBorrador" b
                SET "Estado" = 3, "FacturaVentaNumero" = c."Documento", "IsDeleted" = false,
                    "UpdatedAtUtc" = b."DeletedAtUtc", "UpdatedBy" = 'migracion:enlace-borrador:' || b."Estado",
                    "DeletedAtUtc" = NULL, "DeletedBy" = NULL
                FROM candidatos c
                WHERE b."Id" = c."Id";

                WITH candidatos AS (
                    SELECT b."Id", n."Numero" AS "Documento", n."CreatedAtUtc" AS "PosteadoEn", b."DeletedAtUtc" AS "BorradoEn"
                    FROM "NotasCreditoVentaBorrador" b
                    JOIN "NotasCreditoVenta" n ON n."NumeroBorrador" = b."Numero" AND n."SocioNegocioId" = b."SocioNegocioId"
                                              AND n."FacturaVentaNumero" = b."FacturaVentaNumero"
                    WHERE b."IsDeleted" = true AND b."NotaCreditoVentaNumero" IS NULL AND b."DeletedAtUtc" >= n."CreatedAtUtc"
                      AND NOT EXISTS (SELECT 1 FROM "NotasCreditoVentaBorrador" o WHERE o."Numero" = b."Numero" AND o."Id" <> b."Id")
                      AND NOT EXISTS (SELECT 1 FROM "NotasCreditoVentaBorrador" o WHERE o."NotaCreditoVentaNumero" = n."Numero" AND o."IsDeleted" = false)
                ),
                lineas AS (
                    UPDATE "LineasNotaCreditoVentaBorrador" l
                    SET "IsDeleted" = false, "UpdatedAtUtc" = l."DeletedAtUtc", "UpdatedBy" = 'migracion:enlace-borrador',
                        "DeletedAtUtc" = NULL, "DeletedBy" = NULL
                    FROM candidatos c
                    WHERE l."NotaCreditoVentaBorradorId" = c."Id" AND l."IsDeleted" = true
                      AND l."DeletedAtUtc" >= c."PosteadoEn" AND l."DeletedAtUtc" <= c."BorradoEn"
                    RETURNING l."Id"
                )
                UPDATE "NotasCreditoVentaBorrador" b
                SET "Estado" = 3, "NotaCreditoVentaNumero" = c."Documento", "IsDeleted" = false,
                    "UpdatedAtUtc" = b."DeletedAtUtc", "UpdatedBy" = 'migracion:enlace-borrador:' || b."Estado",
                    "DeletedAtUtc" = NULL, "DeletedBy" = NULL
                FROM candidatos c
                WHERE b."Id" = c."Id";
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Deshace exactamente las filas marcadas por Up: vuelven a borradas con su <c>DeletedAtUtc</c> original (guardado en
        /// <c>UpdatedAtUtc</c>), sin enlace y con su estado original (sufijo de la marca). El usuario original de <c>DeletedBy</c> no se
        /// conserva: queda <c>'migracion'</c>, como en el Down de <c>BorradoresConSeriesYPosteada</c>.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "LineasFacturaVentaBorrador" l
                SET "IsDeleted" = true, "DeletedAtUtc" = l."UpdatedAtUtc", "DeletedBy" = 'migracion', "UpdatedBy" = 'migracion'
                FROM "FacturasVentaBorrador" b
                WHERE l."FacturaVentaBorradorId" = b."Id" AND l."UpdatedBy" = 'migracion:enlace-borrador' AND l."IsDeleted" = false
                  AND b."UpdatedBy" LIKE 'migracion:enlace-borrador:%';
                UPDATE "FacturasVentaBorrador"
                SET "IsDeleted" = true, "DeletedAtUtc" = "UpdatedAtUtc", "DeletedBy" = 'migracion', "FacturaVentaNumero" = NULL,
                    "Estado" = substr("UpdatedBy", length('migracion:enlace-borrador:') + 1)::smallint, "UpdatedBy" = 'migracion'
                WHERE "UpdatedBy" LIKE 'migracion:enlace-borrador:%' AND "IsDeleted" = false;

                UPDATE "LineasNotaCreditoVentaBorrador" l
                SET "IsDeleted" = true, "DeletedAtUtc" = l."UpdatedAtUtc", "DeletedBy" = 'migracion', "UpdatedBy" = 'migracion'
                FROM "NotasCreditoVentaBorrador" b
                WHERE l."NotaCreditoVentaBorradorId" = b."Id" AND l."UpdatedBy" = 'migracion:enlace-borrador' AND l."IsDeleted" = false
                  AND b."UpdatedBy" LIKE 'migracion:enlace-borrador:%';
                UPDATE "NotasCreditoVentaBorrador"
                SET "IsDeleted" = true, "DeletedAtUtc" = "UpdatedAtUtc", "DeletedBy" = 'migracion', "NotaCreditoVentaNumero" = NULL,
                    "Estado" = substr("UpdatedBy", length('migracion:enlace-borrador:') + 1)::smallint, "UpdatedBy" = 'migracion'
                WHERE "UpdatedBy" LIKE 'migracion:enlace-borrador:%' AND "IsDeleted" = false;
                """);
        }
    }
}
