using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <summary>
    /// Invariante dura del spec 5.5 en la propia base (ronda 1 de la Task 5.5): "el commit falla si el registro no cuadra".
    /// Un constraint trigger DEFERRABLE INITIALLY DEFERRED sobre MovimientosContables comprueba, al COMMIT, que la suma de
    /// Importe de cada RegistroContableId insertado en la transacción es 0; si no, el COMMIT falla con SQLSTATE 23514
    /// (check_violation) y la transacción entera se deshace, aunque un llamador haya capturado la excepción de
    /// IRegistroContable y confirmado igualmente. Sin cambios de modelo (EF no modela triggers).
    /// </summary>
    public partial class VerificarCuadreLibroContable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION libro_contable_verificar_cuadre() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE
                    suma numeric;
                BEGIN
                    SELECT COALESCE(SUM("Importe"), 0) INTO suma
                    FROM "MovimientosContables" WHERE "RegistroContableId" = NEW."RegistroContableId";
                    IF suma <> 0 THEN
                        RAISE EXCEPTION 'El registro contable % no cuadra: SUM(Importe) = %', NEW."RegistroContableId", suma
                            USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END $$;
                """);

            // Constraint trigger: solo FOR EACH ROW (Postgres no admite constraint triggers de sentencia). Se evalúa una vez
            // por fila insertada, al commit; cada evaluación usa IX_MovimientosContables_RegistroContableId.
            migrationBuilder.Sql("""
                CREATE CONSTRAINT TRIGGER "TR_MovimientosContables_Cuadre"
                    AFTER INSERT ON "MovimientosContables"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION libro_contable_verificar_cuadre();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_MovimientosContables_Cuadre\" ON \"MovimientosContables\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS libro_contable_verificar_cuadre();");
        }
    }
}
