namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Red permanente (Task 3.8) para la regla "el libro de inventario es append-only": los triggers
/// <c>libro_inventario_append_only</c> de la Task 3.3 ya lo garantizan en tiempo de ejecución contra
/// Postgres real (ver <see cref="LibroInventarioAppendOnlyTests"/>), pero este test lo comprueba también
/// en el código fuente, sin Docker: recorre todo <c>src/**/*.cs</c> (excepto <c>Migrations/</c>, que
/// contiene el DDL de los propios triggers y, en <c>ReemplazarStockPorLibro</c>, un <c>UPDATE</c> legítimo
/// sobre <c>Productos</c>, no sobre el libro) y falla si alguna línea que mencione <c>MovimientosValor</c>
/// o <c>AplicacionesMovimientoProducto</c> contiene <c>UPDATE </c>, <c>DELETE </c>, <c>.Remove(</c>,
/// <c>.RemoveRange(</c>, <c>.Update(</c>, <c>ExecuteDelete</c> o <c>ExecuteUpdate</c>.
/// <para>
/// Deliberadamente NO incluye <c>MovimientosProducto</c>: esa tabla sí tiene un UPDATE legítimo y
/// documentado (el decremento de <c>CantidadRestante</c> por la aplicación FIFO de la Task 3.4), que el
/// propio trigger permite como única excepción. <c>MovimientosValor</c> y
/// <c>AplicacionesMovimientoProducto</c> no tienen ninguna excepción: son estrictamente append-only.
/// </para>
/// </summary>
public sealed class LibroInventarioSinUpdateNiDeleteTests
{
    private static readonly string[] TablasProtegidas = ["MovimientosValor", "AplicacionesMovimientoProducto"];

    private static readonly string[] PatronesProhibidos =
    [
        "UPDATE ",
        "DELETE ",
        ".Remove(",
        ".RemoveRange(",
        ".Update(",
        "ExecuteDelete",
        "ExecuteUpdate"
    ];

    [Fact]
    public void NingunArchivoFueraDeMigrations_ActualizaOBorraMovimientosValorOAplicaciones()
    {
        var raiz = EncontrarRaizDelRepositorio();
        var carpetaSrc = Path.Combine(raiz, "src");

        var archivos = Directory.EnumerateFiles(carpetaSrc, "*.cs", SearchOption.AllDirectories)
            .Where(archivo => !EstaDentroDeMigrations(archivo, carpetaSrc))
            .ToList();

        Assert.NotEmpty(archivos); // Sanity: si esto viene vacío, el filtro de arriba está mal y el test no comprueba nada.

        var violaciones = new List<string>();

        foreach (var archivo in archivos)
        {
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i];
                if (!TablasProtegidas.Any(linea.Contains))
                {
                    continue;
                }

                var patronEncontrado = PatronesProhibidos.FirstOrDefault(linea.Contains);
                if (patronEncontrado is not null)
                {
                    violaciones.Add($"{Path.GetRelativePath(raiz, archivo)}:{i + 1}: contiene '{patronEncontrado}' junto a una tabla append-only -> {linea.Trim()}");
                }
            }
        }

        Assert.True(violaciones.Count == 0,
            "Se encontró código fuera de Migrations/ que actualiza o borra el libro de inventario " +
            "(MovimientosValor/AplicacionesMovimientoProducto son append-only):\n" + string.Join('\n', violaciones));
    }

    private static bool EstaDentroDeMigrations(string archivo, string carpetaSrc)
    {
        var relativo = Path.GetRelativePath(carpetaSrc, archivo);
        var segmentos = relativo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segmentos.Contains("Migrations");
    }

    /// <summary>
    /// Sube desde <see cref="AppContext.BaseDirectory"/> (algo como
    /// <c>tests/OpenSource1.SmokeTests/bin/Debug/net10.0/</c>) hasta encontrar <c>test.slnx</c>, para no
    /// depender de rutas relativas frágiles al directorio de ejecución de los tests.
    /// </summary>
    private static string EncontrarRaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "test.slnx")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException(
            $"No se encontró 'test.slnx' subiendo desde {AppContext.BaseDirectory}.");
    }
}
