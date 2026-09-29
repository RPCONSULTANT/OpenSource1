using System.Text.RegularExpressions;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Red permanente (Task 3.8, endurecida en la revisión final de la Fase 3) para la regla "el libro de inventario es
/// append-only": los triggers <c>libro_inventario_append_only</c> de la Task 3.3 ya lo garantizan en tiempo de
/// ejecución contra Postgres real (ver <see cref="LibroInventarioAppendOnlyTests"/>), pero este test lo comprueba
/// también en el código fuente, sin Docker: escanea el CONTENIDO COMPLETO (no línea a línea, para que una sentencia
/// partida en dos líneas no se escape del detector) de todo <c>src/**/*.cs</c> (excepto <c>Migrations/</c>, que
/// contiene el DDL de los propios triggers y, en <c>ReemplazarStockPorLibro</c>, un <c>UPDATE</c> legítimo sobre
/// <c>Productos</c>, no sobre el libro) buscando, de forma insensible a mayúsculas:
/// <list type="bullet">
/// <item>SQL <c>UPDATE</c>/<c>DELETE FROM</c>/<c>TRUNCATE [TABLE]</c> cuyo objetivo sea <c>"MovimientosValor"</c> o
/// <c>"AplicacionesMovimientoProducto"</c> (comillas opcionales, <c>\s+</c> entre la palabra clave y el nombre para
/// tolerar saltos de línea entre medias).</item>
/// <item><c>Set&lt;MovimientoValor&gt;</c>/<c>Set&lt;AplicacionMovimientoProducto&gt;</c> o los DbSets
/// <c>MovimientosValor</c>/<c>AplicacionesMovimientoProducto</c>, seguidos —en la misma expresión, sin cruzar un
/// <c>;</c>— de <c>.Remove</c>/<c>.RemoveRange</c>/<c>.Update</c>/<c>.UpdateRange</c>/<c>ExecuteDelete</c>/
/// <c>ExecuteUpdate</c>.</item>
/// </list>
/// <para>
/// Deliberadamente NO incluye <c>MovimientosProducto</c>: esa tabla sí tiene un UPDATE legítimo y documentado (el
/// decremento de <c>CantidadRestante</c> por la aplicación FIFO de la Task 3.4), que el propio trigger permite como
/// única excepción. <c>MovimientosValor</c>, <c>AplicacionesMovimientoProducto</c> y <c>RegistrosDiario</c> (Task 4.5:
/// agregada a esta red, mismo trigger <c>libro_inventario_append_only()</c> reutilizado por la migración
/// <c>AddRegistrosDiario</c> de la Fase 4) no tienen ninguna excepción: son estrictamente append-only. La Task 5.5 añade el
/// libro contable, <c>MovimientosContables</c> y <c>RegistrosContables</c> (mismo trigger, migración <c>AddLibroContable</c>),
/// también sin excepción. La Task 5.6 abre UNA excepción en <c>MovimientosValor</c>, la misma que el trigger (migración
/// <c>PermitirContabilizacionCosto</c>): el <c>UPDATE "MovimientosValor" SET "ImporteCostoPosteadoContabilidad" = ... WHERE ...</c>
/// del batch de costo, con esa columna como única asignación (ver <see cref="ExcepcionPosteoCosto"/>). La Task 6.3 añade el
/// documento de venta posteado (<c>FacturasVenta</c>, <c>LineasFacturaVenta</c>, <c>LineasIvaFacturaVenta</c>) y el libro de
/// clientes (<c>MovimientosCliente</c>, <c>MovimientosClienteDetalle</c>), mismo trigger (migración
/// <c>AddFacturasVentaYLibroClientes</c>), sin excepción. Los borradores (<c>FacturasVentaBorrador</c>,
/// <c>LineasFacturaVentaBorrador</c>) NO están protegidos: el <c>\b</c> tras el nombre los distingue. La Task 8.6 añade el documento
/// de nota de crédito posteado (<c>NotasCreditoVenta</c>, <c>LineasNotaCreditoVenta</c>, <c>LineasIvaNotaCreditoVenta</c>; migración
/// <c>AddNotasCreditoVenta</c>), sin excepción; sus borradores tampoco están protegidos.
/// </para>
/// </summary>
public sealed class LibroInventarioSinUpdateNiDeleteTests
{
    private static readonly string[] TablasProtegidas =
    [
        "MovimientosValor", "AplicacionesMovimientoProducto", "RegistrosDiario", "MovimientosContables", "RegistrosContables",
        "FacturasVenta", "LineasFacturaVenta", "LineasIvaFacturaVenta", "MovimientosCliente", "MovimientosClienteDetalle",
        "NotasCreditoVenta", "LineasNotaCreditoVenta", "LineasIvaNotaCreditoVenta",
    ];
    private static readonly string[] EntidadesProtegidas =
    [
        "MovimientoValor", "AplicacionMovimientoProducto", "RegistroDiario", "MovimientoContable", "RegistroContable",
        "FacturaVenta", "LineaFacturaVenta", "LineaIvaFacturaVenta", "MovimientoCliente", "MovimientoClienteDetalle",
        "NotaCreditoVenta", "LineaNotaCreditoVenta", "LineaIvaNotaCreditoVenta",
    ];

    /// <summary>
    /// SQL prohibido: la palabra clave, uno o más espacios/saltos de línea (<c>\s+</c>, insensible a mayúsculas) y el
    /// nombre de la tabla, con comillas opcionales. El <c>\b</c> va INMEDIATAMENTE después del nombre (no después de
    /// una comilla de cierre opcional): un <c>\b</c> tras la comilla fallaría si el carácter siguiente también es
    /// "no palabra" (un espacio), porque ninguno de los dos lados sería \w.
    /// </summary>
    /// <remarks>
    /// Task 5.6 (desviación de la Fase 5): la ÚNICA excepción es el <c>UPDATE "MovimientosValor" SET
    /// "ImporteCostoPosteadoContabilidad" = &lt;expresión&gt; WHERE ...</c> del batch de costo, con esa columna como única
    /// asignación: la expresión hasta el <c>WHERE</c> no puede contener una coma (ni un <c>;</c>), así que asignar además otra
    /// columna (antes o después) sigue detectándose. El grupo atómico <c>(?&gt;"?)</c> impide que el motor "devuelva" la comilla de
    /// cierre para que la búsqueda negativa empiece en ella y deje pasar la excepción por accidente.
    /// Task 6.3: la comilla opcional admite también la forma escapada de un literal C# normal (<c>\"</c>): antes
    /// <c>"UPDATE \"MovimientosClienteDetalle\" ..."</c> no se detectaba (solo los literales raw o sin comillas).
    /// </remarks>
    private const string ExcepcionPosteoCosto =
        """(?!\s+SET\s+(?:\\?")?ImporteCostoPosteadoContabilidad\b(?:\\?")?\s*=[^,;]*?\bWHERE\b)""";

    private static readonly Regex PatronSql = new(
        string.Join("|", TablasProtegidas.Select(tabla => tabla == "MovimientosValor"
            ? $"""(?:DELETE\s+FROM|TRUNCATE(?:\s+TABLE)?)\s+(?:\\?")?MovimientosValor\b|UPDATE\s+(?:\\?")?MovimientosValor\b(?>(?:\\?")?){ExcepcionPosteoCosto}"""
            : $"""(?:UPDATE|DELETE\s+FROM|TRUNCATE(?:\s+TABLE)?)\s+(?:\\?")?{Regex.Escape(tabla)}\b""")),
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Código prohibido: <c>Set&lt;Entidad&gt;()</c> o el DbSet (con o sin punto delante, para cubrir tanto
    /// <c>context.MovimientosValor</c> como un uso interno sin calificar), seguido -en una ventana acotada que NO
    /// cruza un <c>;</c>, para quedarse dentro de la misma expresión/statement- de un método mutador. La ventana
    /// permite una cadena LINQ intermedia (p. ej. <c>.Where(...)</c> antes de <c>ExecuteDeleteAsync()</c>).
    /// Task 6.3: el <c>&gt;</c> de <c>Set&lt;...&gt;</c> va FUERA de la alternancia de entidades; antes quedaba dentro de la
    /// última alternativa y solo se detectaba <c>Set&lt;&gt;</c> de la última entidad de la lista (lo destapó la mutación de
    /// control <c>Set&lt;LineaIvaFacturaVenta&gt;().Update</c>).
    /// </summary>
    private static readonly Regex PatronCodigo = new(
        """(?:Set<(?:""" + string.Join("|", EntidadesProtegidas.Select(Regex.Escape)) + """)>\s*\(\s*\)|\.?(?:""" +
        string.Join("|", TablasProtegidas.Select(Regex.Escape)) +
        """)\b)(?:(?!;)[\s\S]){0,300}?\.\s*(?:Remove|RemoveRange|Update|UpdateRange|ExecuteDelete\w*|ExecuteUpdate\w*)\s*\(""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void NingunArchivoFueraDeMigrations_ActualizaOBorraMovimientosValorOAplicaciones()
    {
        var raiz = BlazorSsrFactory.RaizRepositorio();
        var carpetaSrc = Path.Combine(raiz, "src");

        var archivos = Directory.EnumerateFiles(carpetaSrc, "*.cs", SearchOption.AllDirectories)
            .Where(archivo => !EstaDentroDeMigrations(archivo, carpetaSrc))
            .ToList();

        Assert.NotEmpty(archivos); // Sanity: si esto viene vacío, el filtro de arriba está mal y el test no comprueba nada.

        var violaciones = new List<string>();

        foreach (var archivo in archivos)
        {
            var contenido = File.ReadAllText(archivo);
            foreach (var violacion in Violaciones(contenido))
            {
                violaciones.Add($"{Path.GetRelativePath(raiz, archivo)}:{NumeroDeLinea(contenido, violacion.Indice)}: {violacion.Descripcion}");
            }
        }

        Assert.True(violaciones.Count == 0,
            "Se encontró código fuera de Migrations/ que actualiza o borra el libro de inventario " +
            $"({string.Join('/', TablasProtegidas)} son append-only):\n" + string.Join('\n', violaciones));
    }

    // Mutaciones de control (revisión final de la Fase 3, ampliadas en la Task 4.5): sin estos cinco casos
    // sintéticos el detector podría parecer correcto y sin embargo dejar pasar justo lo que motivó el
    // endurecimiento del test original (que trabajaba línea a línea). Contenido de archivo simulado, no C# que
    // deba compilar.
    public static TheoryData<string> MutacionesDeControl => new()
    {
        // SQL en una sola línea.
        """UPDATE "MovimientosValor" SET "ImporteCosto" = 0 WHERE "Id" = 1;""",
        // SQL partido en dos líneas y en minúsculas: el "\s+" original insensible a mayúsculas ya lo cubre, pero la
        // implementación anterior (línea a línea) no, porque la palabra clave y el nombre caían en líneas distintas.
        """
        update
        "movimientosvalor" set "importecosto" = 0 where "id" = 1;
        """,
        // Código: DbSet seguido de .Remove en la misma expresión.
        "context.MovimientosValor.Remove(x);",
        // Task 4.5: mismos dos casos para RegistrosDiario (SQL en una línea y código).
        """UPDATE "RegistrosDiario" SET "NumeroRegistro" = 'X' WHERE "Id" = 1;""",
        "context.RegistrosDiario.Remove(x);",
        // Task 5.5: libro contable (SQL partido en líneas, DELETE, TRUNCATE y código por DbSet y por Set<T>()).
        """
        UPDATE
            "MovimientosContables" SET "Importe" = 0 WHERE "Id" = 1;
        """,
        """DELETE FROM "MovimientosContables" WHERE "Id" = 1;""",
        """TRUNCATE TABLE "RegistrosContables";""",
        """UPDATE RegistrosContables SET "NumeroRegistro" = 'X';""",
        "context.MovimientosContables.Where(x => x.Id == 1).ExecuteDeleteAsync();",
        "context.Set<RegistroContable>().Update(x);",
        // Task 5.6: la excepción del batch de costo es SOLO la columna ImporteCostoPosteadoContabilidad; asignar otra columna
        // además (antes o después), otra columna sola, sin comillas, partido en líneas o sin WHERE sigue siendo una violación.
        """UPDATE "MovimientosValor" SET "ImporteCostoPosteadoContabilidad" = "ImporteCosto", "ImporteCosto" = 0 WHERE "Id" = 1;""",
        """UPDATE "MovimientosValor" SET "ImporteCosto" = 0, "ImporteCostoPosteadoContabilidad" = 0 WHERE "Id" = 1;""",
        """UPDATE "MovimientosValor" SET "GrupoInventarioId" = NULL WHERE "Id" = 1;""",
        """
        update movimientosvalor
        set importecostoposteadocontabilidad = 0,
            grupoinventarioid = null where id = 1;
        """,
        """UPDATE "MovimientosValor" SET "ImporteCostoPosteadoContabilidadX" = 0 WHERE "Id" = 1;""",
        """UPDATE "MovimientosValor" SET "ImporteCostoPosteadoContabilidad" = 0;""",
        "context.MovimientosValor.Where(x => x.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImporteCostoPosteadoContabilidad, 0m));",
        // Task 6.3: documento posteado y libro de clientes (SQL en una línea, partido, sin comillas, DELETE, TRUNCATE y código).
        """UPDATE "FacturasVenta" SET "ImporteTotal" = 0 WHERE "Numero" = '00000001';""",
        """
        delete from
            facturasventa where "Numero" = '00000001';
        """,
        """UPDATE "LineasFacturaVenta" SET "ImporteLinea" = 0 WHERE "Id" = 1;""",
        """DELETE FROM "LineasIvaFacturaVenta" WHERE "Id" = 1;""",
        """UPDATE MovimientosCliente SET "ImporteOriginal" = 0;""",
        """TRUNCATE "MovimientosClienteDetalle";""",
        """
        UPDATE
            "MovimientosClienteDetalle" SET "Importe" = 0 WHERE "Id" = 1;
        """,
        "context.FacturasVenta.Remove(x);",
        "context.LineasFacturaVenta.Where(x => x.Id == 1).ExecuteDeleteAsync();",
        "context.Set<LineaIvaFacturaVenta>().Update(x);",
        "context.MovimientosCliente.Where(x => x.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImporteOriginal, 0m));",
        "context.Set<MovimientoClienteDetalle>().RemoveRange(xs);",
        // Task 6.3: Set<T>() de entidades que NO son la última de la lista (el patrón anterior solo cubría la última).
        "context.Set<MovimientoValor>().Remove(x);",
        "context.Set<FacturaVenta>().UpdateRange(xs);",
        // Task 6.3: SQL en un literal C# normal, con las comillas escapadas.
        "const string sql = \"UPDATE \\\"MovimientosClienteDetalle\\\" SET \\\"Importe\\\" = 0\";",
        "const string sql = \"DELETE FROM \\\"MovimientosValor\\\" WHERE \\\"Id\\\" = 1\";",
        "const string sql = \"UPDATE \\\"MovimientosValor\\\" SET \\\"ImporteCosto\\\" = 0 WHERE \\\"Id\\\" = 1\";",
    };

    [Theory]
    [MemberData(nameof(MutacionesDeControl))]
    public void Violaciones_DetectaCadaMutacionDeControl(string contenidoMutado) =>
        Assert.NotEmpty(Violaciones(contenidoMutado));

    [Fact]
    public void Violaciones_NoMarcaLosUsosLegitimosDelCodigoReal()
    {
        // Mismo patrón que el código de producción real: declaración de los DbSets (Set<T>() sin mutador detrás,
        // separado por ";") y un INSERT (permitido: el libro es append-ONLY, no "sin escritura").
        const string contenido = """"
            /// <summary>Ver <c>MovimientosValor</c>, <c>AplicacionesMovimientoProducto</c> y <c>RegistrosDiario</c>.</summary>
            public DbSet<MovimientoValor> MovimientosValor => Set<MovimientoValor>();
            public DbSet<AplicacionMovimientoProducto> AplicacionesMovimientoProducto => Set<AplicacionMovimientoProducto>();
            public DbSet<RegistroDiario> RegistrosDiario => Set<RegistroDiario>();
            public DbSet<MovimientoContable> MovimientosContables => Set<MovimientoContable>();
            public DbSet<RegistroContable> RegistrosContables => Set<RegistroContable>();
            public DbSet<FacturaVenta> FacturasVenta => Set<FacturaVenta>();
            public DbSet<MovimientoClienteDetalle> MovimientosClienteDetalle => Set<MovimientoClienteDetalle>();

            // Los borradores de factura sí se modifican y borran (soft delete): no son tablas protegidas.
            const string sqlB = "UPDATE \"FacturasVentaBorrador\" SET \"Estado\" = 2 WHERE \"Id\" = @Id";
            const string sqlB2 = "DELETE FROM \"LineasFacturaVentaBorrador\" WHERE \"Id\" = @Id";
            context.Set<FacturaVentaBorrador>().Update(x);
            context.LineasFacturaVentaBorrador.Remove(x);
            const string sqlF = "INSERT INTO \"FacturasVenta\" (\"Numero\") VALUES (@Numero)";
            const string sqlF2 = "SELECT COALESCE(SUM(d.\"Importe\"), 0) FROM \"MovimientosClienteDetalle\" d";

            const string sql = "INSERT INTO \"MovimientosValor\" (\"ProductoId\") VALUES (@ProductoId)";
            const string sql2 = "UPDATE \"MovimientosProducto\" SET \"CantidadRestante\" = \"CantidadRestante\" - @Aplicada WHERE \"Id\" = @Id";
            const string sql3 = "INSERT INTO \"RegistrosDiario\" (\"NumeroRegistro\") VALUES (@NumeroRegistro)";
            const string sql4 = "INSERT INTO \"MovimientosContables\" (\"Id\") OVERRIDING SYSTEM VALUE VALUES (@Id)";
            const string sql5 = "SELECT COUNT(*), COALESCE(SUM(\"Importe\"), 0) FROM \"MovimientosContables\" WHERE \"RegistroContableId\" = @Id";
            const string sql6 = """
                UPDATE "MovimientosValor" SET "ImporteCostoPosteadoContabilidad" = "ImporteCosto"
                WHERE "Id" = ANY(@Ids) AND "ImporteCosto" <> "ImporteCostoPosteadoContabilidad"
                """;
            const string sql7 = "update MovimientosValor set ImporteCostoPosteadoContabilidad = 0 where Id = 1";
            const string sql8 = "UPDATE \"MovimientosValor\" SET \"ImporteCostoPosteadoContabilidad\" = 0 WHERE \"Id\" = @Id";
            """";

        Assert.Empty(Violaciones(contenido));
    }

    /// <summary>Escanea TODO el contenido (no línea a línea) y devuelve cada coincidencia con su posición.</summary>
    private static IEnumerable<(int Indice, string Descripcion)> Violaciones(string contenido)
    {
        foreach (Match m in PatronSql.Matches(contenido))
        {
            yield return (m.Index, $"contiene SQL prohibido sobre una tabla append-only -> {Colapsar(m.Value)}");
        }

        foreach (Match m in PatronCodigo.Matches(contenido))
        {
            yield return (m.Index, $"contiene una llamada de código prohibida sobre una tabla append-only -> {Colapsar(m.Value)}");
        }
    }

    private static string Colapsar(string texto) => Regex.Replace(texto, @"\s+", " ").Trim();

    private static int NumeroDeLinea(string contenido, int indice) =>
        contenido.AsSpan(0, indice).Count('\n') + 1;

    private static bool EstaDentroDeMigrations(string archivo, string carpetaSrc)
    {
        var relativo = Path.GetRelativePath(carpetaSrc, archivo);
        var segmentos = relativo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segmentos.Contains("Migrations");
    }
}
