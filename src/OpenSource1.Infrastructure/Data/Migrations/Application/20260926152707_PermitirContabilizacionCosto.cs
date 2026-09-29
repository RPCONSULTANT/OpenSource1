using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <summary>
    /// Task 5.6 (desviación de la Fase 5): <c>ImporteCostoPosteadoContabilidad</c> es la segunda columna actualizable del libro
    /// de valor. La función compartida <c>libro_inventario_append_only()</c> pasa a admitir en <c>MovimientosValor</c> un
    /// <c>UPDATE</c> que cambie ÚNICAMENTE esa columna (misma técnica <c>to_jsonb(NEW) - columna = to_jsonb(OLD) - columna</c>
    /// que ya se usa para <c>CantidadRestante</c> en <c>MovimientosProducto</c>). Todo lo demás sigue prohibido: DELETE, TRUNCATE
    /// (trigger de sentencia, <c>TG_OP = 'TRUNCATE'</c>) y cualquier UPDATE en el resto de tablas del libro (inventario,
    /// registros de diario y libro contable, que reutilizan la función). Sin cambios de modelo.
    /// </summary>
    public partial class PermitirContabilizacionCosto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION libro_inventario_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'El libro % es de solo inserción: DELETE no permitido', TG_TABLE_NAME USING ERRCODE = 'P0001';
                    END IF;
                    IF TG_OP = 'UPDATE' AND TG_TABLE_NAME = 'MovimientosProducto'
                       AND (to_jsonb(NEW) - 'CantidadRestante') = (to_jsonb(OLD) - 'CantidadRestante') THEN
                        RETURN NEW;
                    END IF;
                    IF TG_OP = 'UPDATE' AND TG_TABLE_NAME = 'MovimientosValor'
                       AND (to_jsonb(NEW) - 'ImporteCostoPosteadoContabilidad') = (to_jsonb(OLD) - 'ImporteCostoPosteadoContabilidad') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'El libro % es de solo inserción: UPDATE no permitido', TG_TABLE_NAME USING ERRCODE = 'P0001';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Versión de AddLibroInventario (solo CantidadRestante en MovimientosProducto).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION libro_inventario_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'El libro % es de solo inserción: DELETE no permitido', TG_TABLE_NAME USING ERRCODE = 'P0001';
                    END IF;
                    IF TG_TABLE_NAME = 'MovimientosProducto'
                       AND (to_jsonb(NEW) - 'CantidadRestante') = (to_jsonb(OLD) - 'CantidadRestante') THEN
                        RETURN NEW;
                    END IF;
                    RAISE EXCEPTION 'El libro % es de solo inserción: UPDATE no permitido', TG_TABLE_NAME USING ERRCODE = 'P0001';
                END $$;
                """);
        }
    }
}
