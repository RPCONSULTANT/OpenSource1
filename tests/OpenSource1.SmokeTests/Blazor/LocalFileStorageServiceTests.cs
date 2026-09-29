extern alias BlazorApp;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using OpenSource1.Application.Storage;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// El borrado de imágenes es una operación destructiva alimentada por una ruta que llega desde la base de datos (y, por tanto,
/// desde cualquier cliente de la API): estos tests comprueban que ninguna ruta hostil borra un fichero señuelo y que el
/// borrado legítimo sigue funcionando.
/// </summary>
public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "ax-storage-" + Guid.NewGuid().ToString("N"));
    private readonly string _contentRoot;
    private readonly string _fueraDeUploads;
    private readonly ListaLogger _logger = new();
    private readonly LocalFileStorageService _servicio;

    public LocalFileStorageServiceTests()
    {
        _contentRoot = Path.Combine(_raiz, "app");
        _fueraDeUploads = Path.Combine(_raiz, "fuera");
        foreach (var carpeta in new[] { RutaImagen.CarpetaClientes, RutaImagen.CarpetaProductos, RutaImagen.CarpetaUsuarios })
        {
            Directory.CreateDirectory(Path.Combine(_contentRoot, "storage", "uploads", carpeta));
        }

        Directory.CreateDirectory(_fueraDeUploads);
        _servicio = new LocalFileStorageService(new EntornoFalso(_contentRoot), _logger);
    }

    public void Dispose()
    {
        try { Directory.Delete(_raiz, recursive: true); } catch (IOException) { }
    }

    private string Uploads(string carpeta, string nombre = "") => Path.Combine(_contentRoot, "storage", "uploads", carpeta, nombre);

    private string Senuelo(string ruta, string contenido = "señuelo")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, contenido);
        return ruta;
    }

    [Fact]
    public async Task Borrado_Legitimo_BorraLaImagenPropiaDeLaCarpetaCorrecta()
    {
        var fichero = Senuelo(Uploads("clientes", "cliente-0123456789abcdef0123456789abcdef.png"));

        await _servicio.DeleteIfExistsAsync("/uploads/clientes/cliente-0123456789abcdef0123456789abcdef.png", RutaImagen.CarpetaClientes);

        Assert.False(File.Exists(fichero));
        Assert.Empty(_logger.Avisos);
    }

    [Theory]
    [InlineData(RutaImagen.CarpetaClientes, "cliente-0123456789abcdef0123456789abcdef.png")]
    [InlineData(RutaImagen.CarpetaClientes, "cliente-0123456789abcdef0123456789abcdef.webp")]
    [InlineData(RutaImagen.CarpetaProductos, "producto-0123456789abcdef0123456789abcdef.jpg")]
    [InlineData(RutaImagen.CarpetaUsuarios, "perfil-0123456789abcdef0123456789abcdef.jpeg")]
    public async Task Borrado_Legitimo_FuncionaEnCadaCarpeta(string carpeta, string nombre)
    {
        var fichero = Senuelo(Uploads(carpeta, nombre));

        await _servicio.DeleteIfExistsAsync($"/uploads/{carpeta}/{nombre}", carpeta);

        Assert.False(File.Exists(fichero));
    }

    [Fact]
    public async Task Borrado_DeFicheroInexistenteOCadenaVacia_NoHaceNadaNiLanza()
    {
        await _servicio.DeleteIfExistsAsync("/uploads/clientes/no-existe.png", RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync(null, RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync("", RutaImagen.CarpetaClientes);
    }

    public static TheoryData<string> RutasHostilesDesdeClientes => new()
    {
        "../fuera/senuelo.txt",
        "..\\fuera\\senuelo.txt",
        "../../../fuera/senuelo.txt",
        "/uploads/clientes/../../../../fuera/senuelo.txt",
        "/uploads/clientes/../../../fuera/senuelo.txt",
        "/uploads/clientes/..\\..\\..\\..\\fuera\\senuelo.txt",
        "/uploads/clientes/../senuelo-users.txt",
        "/uploads/clientes/../users/senuelo-users.txt",
        "/uploads/users/senuelo-users.txt",
        "/uploads/productos/senuelo-productos.txt",
        "/uploads/clientes/sub/senuelo-sub.txt",
        "/uploads/clientes//" + "{FUERA}",
        "/uploads/clientes/%2e%2e/%2e%2e/senuelo.txt",
        "/uploads/clientes/%2e%2e%2fsenuelo-users.txt",
        "/uploads/clientes/appsettings.json/../../../../appsettings.json",
        "{FUERA}",
        "senuelo-raiz.txt",
        "appsettings.json",
        "/appsettings.json",
        "uploads/clientes/senuelo-clientes.txt",
        "/UPLOADS/clientes/senuelo-clientes.txt",
        "/uploads/clientes",
        "/uploads/clientes/",
        "/uploads/clientes/.",
        "/uploads/clientes/..",
        "",
        "   ",
    };

    [Theory]
    [MemberData(nameof(RutasHostilesDesdeClientes))]
    public async Task Borrado_RutaHostil_NoBorraNingunSenuelo(string ruta)
    {
        var fuera = Senuelo(Path.Combine(_fueraDeUploads, "senuelo.txt"));
        var users = Senuelo(Uploads("users", "senuelo-users.txt"));
        var productos = Senuelo(Uploads("productos", "senuelo-productos.txt"));
        var subdir = Senuelo(Uploads("clientes", Path.Combine("sub", "senuelo-sub.txt")));
        var enClientes = Senuelo(Uploads("clientes", "senuelo-clientes.txt"));
        var raiz = Senuelo(Path.Combine(_contentRoot, "senuelo-raiz.txt"));
        var settings = Senuelo(Path.Combine(_contentRoot, "appsettings.json"), "{}");
        var storage = Senuelo(Path.Combine(_contentRoot, "storage", "senuelo-storage.txt"));

        await _servicio.DeleteIfExistsAsync(ruta.Replace("{FUERA}", fuera), RutaImagen.CarpetaClientes);

        foreach (var f in new[] { fuera, users, productos, subdir, raiz, settings, storage })
        {
            Assert.True(File.Exists(f), $"La ruta '{ruta}' borró el señuelo {f}");
        }

        // Solo el que está en la carpeta de clientes y se nombra exactamente puede desaparecer; aquí ninguna ruta lo nombra.
        Assert.True(File.Exists(enClientes));
    }

    [Theory]
    [InlineData("senuelo-clientes.txt")]
    [InlineData("decoy")]
    [InlineData("cliente-1.png")]
    [InlineData("cliente-0123456789ABCDEF0123456789ABCDEF.png")]
    [InlineData("cliente-0123456789abcdef0123456789abcdef.txt")]
    [InlineData("producto-0123456789abcdef0123456789abcdef.png")]
    public async Task Borrado_DeUnFicheroDeLaPropiaCarpetaQueLaAplicacionNoGenero_NoBorra(string nombre)
    {
        // Otro fichero de la misma carpeta (por ejemplo, la imagen de otro registro o un archivo ajeno): no se toca.
        var fichero = Senuelo(Uploads("clientes", nombre));

        await _servicio.DeleteIfExistsAsync($"/uploads/clientes/{nombre}", RutaImagen.CarpetaClientes);

        Assert.True(File.Exists(fichero));
        Assert.Contains(_logger.Avisos, a => a.Contains("rechazado"));
    }

    // Señuelos con el NOMBRE GENERADO por el servicio (cliente-<32 hex>.png): el filtro de nombre no los tapa, así que estos
    // tests aíslan la contención de ruta (carpeta de la entidad + hijo directo). Sin ella, todos fallan.
    private const string NombreGenerado = "cliente-fedcba9876543210fedcba9876543210.png";

    [Fact]
    public async Task Borrado_SenueloConNombreGenerado_FueraDeUploads_AlcanzadoConPuntosYConRutaAbsoluta_NoSeBorra()
    {
        var fuera = Senuelo(Path.Combine(_fueraDeUploads, NombreGenerado));
        var enRaiz = Senuelo(Path.Combine(_raiz, NombreGenerado));
        var enContentRoot = Senuelo(Path.Combine(_contentRoot, NombreGenerado));
        var enStorage = Senuelo(Path.Combine(_contentRoot, "storage", NombreGenerado));
        var enUploads = Senuelo(Path.Combine(_contentRoot, "storage", "uploads", NombreGenerado));

        foreach (var ruta in new[]
        {
            $"/uploads/clientes/../../../../fuera/{NombreGenerado}",
            $"/uploads/clientes/..\\..\\..\\..\\fuera\\{NombreGenerado}",
            $"/uploads/clientes/../../../../{NombreGenerado}",
            $"/uploads/clientes/../../../{NombreGenerado}",
            $"/uploads/clientes/../../{NombreGenerado}",
            $"/uploads/clientes/../{NombreGenerado}",
            "/uploads/clientes/" + fuera,
            "/uploads/clientes/" + enContentRoot,
            "/uploads/clientes//" + fuera,
        })
        {
            await _servicio.DeleteIfExistsAsync(ruta, RutaImagen.CarpetaClientes);
        }

        foreach (var f in new[] { fuera, enRaiz, enContentRoot, enStorage, enUploads })
        {
            Assert.True(File.Exists(f), $"Se borró el señuelo con nombre generado {f}");
        }
    }

    [Fact]
    public async Task Borrado_SenueloConNombreGenerado_EnLaCarpetaDeOtraEntidad_NoSeBorra()
    {
        var enUsers = Senuelo(Uploads("users", NombreGenerado));
        var enProductos = Senuelo(Uploads("productos", NombreGenerado));

        await _servicio.DeleteIfExistsAsync($"/uploads/clientes/../users/{NombreGenerado}", RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync($"/uploads/clientes/../productos/{NombreGenerado}", RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync($"/uploads/users/{NombreGenerado}", RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync("/uploads/clientes/" + enUsers, RutaImagen.CarpetaClientes);

        Assert.True(File.Exists(enUsers));
        Assert.True(File.Exists(enProductos));
    }

    [Fact]
    public async Task Borrado_SenueloConNombreGenerado_EnUnSubdirectorioDeLaPropiaCarpeta_NoSeBorra()
    {
        var enSub = Senuelo(Uploads("clientes", Path.Combine("sub", NombreGenerado)));
        var enSubSub = Senuelo(Uploads("clientes", Path.Combine("sub", "otro", NombreGenerado)));

        await _servicio.DeleteIfExistsAsync($"/uploads/clientes/sub/{NombreGenerado}", RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync($"/uploads/clientes/sub/otro/{NombreGenerado}", RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync($"/uploads/clientes/sub/../sub/{NombreGenerado}", RutaImagen.CarpetaClientes);

        Assert.True(File.Exists(enSub));
        Assert.True(File.Exists(enSubSub));
    }

    [Fact]
    public async Task Borrado_RutaAbsolutaAlSenuelo_NoBorraYRegistraUnAviso()
    {
        var fuera = Senuelo(Path.Combine(_fueraDeUploads, "abs.txt"));

        await _servicio.DeleteIfExistsAsync(fuera, RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync("/uploads/clientes/" + fuera, RutaImagen.CarpetaClientes);

        Assert.True(File.Exists(fuera));
        Assert.Equal(2, _logger.Avisos.Count);
        Assert.All(_logger.Avisos, a => Assert.Contains("rechazado", a));
    }

    [Fact]
    public async Task Borrado_DeOtraEntidad_NoBorra_AunqueLaRutaSeaValidaParaEsaOtraEntidad()
    {
        var usuario = Senuelo(Uploads("users", "perfil-0123456789abcdef0123456789abcdef.png"));
        var producto = Senuelo(Uploads("productos", "producto-0123456789abcdef0123456789abcdef.png"));

        // Un cliente no puede borrar la imagen de un usuario ni de un producto (nombres con la forma legítima de cada una).
        await _servicio.DeleteIfExistsAsync("/uploads/users/perfil-0123456789abcdef0123456789abcdef.png", RutaImagen.CarpetaClientes);
        await _servicio.DeleteIfExistsAsync("/uploads/productos/producto-0123456789abcdef0123456789abcdef.png", RutaImagen.CarpetaClientes);
        // Y un usuario no puede borrar la de un cliente.
        var cliente = Senuelo(Uploads("clientes", "cliente-0123456789abcdef0123456789abcdef.png"));
        await _servicio.DeleteIfExistsAsync("/uploads/clientes/cliente-0123456789abcdef0123456789abcdef.png", RutaImagen.CarpetaUsuarios);

        Assert.True(File.Exists(usuario));
        Assert.True(File.Exists(producto));
        Assert.True(File.Exists(cliente));
    }

    [Fact]
    public async Task Borrado_DeEnlaceSimbolicoDentroDeLaCarpeta_NoSeSigueNiSeBorra()
    {
        var objetivo = Senuelo(Path.Combine(_fueraDeUploads, "objetivo.txt"));
        var enlace = Uploads("clientes", "cliente-0123456789abcdef0123456789abcdef.png");
        try
        {
            File.CreateSymbolicLink(enlace, objetivo);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return; // El entorno no permite crear enlaces simbólicos (p. ej. Windows sin privilegios).
        }

        await _servicio.DeleteIfExistsAsync("/uploads/clientes/cliente-0123456789abcdef0123456789abcdef.png", RutaImagen.CarpetaClientes);

        Assert.True(File.Exists(objetivo), "El objetivo del enlace fue borrado.");
        Assert.True(new FileInfo(enlace).LinkTarget is not null, "El enlace fue borrado.");
        Assert.Contains(_logger.Avisos, a => a.Contains("enlace simbólico"));
    }

    [Fact]
    public async Task Borrado_ATravesDeUnDirectorioEnlazado_NoBorraFueraDeUploads()
    {
        var objetivo = Senuelo(Path.Combine(_fueraDeUploads, "dentro-de-dir-enlazado.txt"));
        var dirEnlace = Uploads("clientes", "dir-enlazado");
        try
        {
            Directory.CreateSymbolicLink(dirEnlace, _fueraDeUploads);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        await _servicio.DeleteIfExistsAsync("/uploads/clientes/dir-enlazado/dentro-de-dir-enlazado.txt", RutaImagen.CarpetaClientes);

        Assert.True(File.Exists(objetivo));
    }

    [Fact]
    public async Task Guardar_SinFicheroSubido_DevuelveLaRutaVigenteValida_YDescartaUnaHostil()
    {
        var contexto = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        contexto.Request.ContentType = "multipart/form-data; boundary=x";
        contexto.Request.Form = new Microsoft.AspNetCore.Http.FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());

        Assert.Equal("/uploads/clientes/cliente-1.png", await _servicio.SaveClientImageAsync(contexto, "ClientImage", "/uploads/clientes/cliente-1.png"));
        Assert.Null(await _servicio.SaveClientImageAsync(contexto, "ClientImage", "../../etc/passwd"));
        Assert.Null(await _servicio.SaveClientImageAsync(contexto, "ClientImage", "/uploads/users/perfil-1.png"));
        Assert.Null(await _servicio.SaveClientImageAsync(contexto, "ClientImage", null));
    }

    [Fact]
    public async Task Guardar_ConFichero_NoBorraLaAnterior_HastaQueQuienLlamaLoConfirme()
    {
        var anterior = Senuelo(Uploads("clientes", "cliente-0123456789abcdef0123456789abcdef.png"));
        var contexto = ContextoConImagen("ClientImage", "nueva.png");

        var nueva = await _servicio.SaveClientImageAsync(contexto, "ClientImage", "/uploads/clientes/cliente-0123456789abcdef0123456789abcdef.png");

        Assert.NotNull(nueva);
        Assert.StartsWith("/uploads/clientes/cliente-", nueva);
        Assert.True(RutaImagen.EsValida(nueva, RutaImagen.CarpetaClientes));
        Assert.True(File.Exists(anterior));
        Assert.True(File.Exists(Uploads("clientes", RutaImagen.NombreDe(nueva!, RutaImagen.CarpetaClientes))));

        // Flujo completo del reemplazo: la API confirma y entonces se retira la anterior.
        await _servicio.DeleteIfExistsAsync("/uploads/clientes/cliente-0123456789abcdef0123456789abcdef.png", RutaImagen.CarpetaClientes);
        Assert.False(File.Exists(anterior));
    }

    private static Microsoft.AspNetCore.Http.HttpContext ContextoConImagen(string campo, string nombre)
    {
        var contexto = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var fichero = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes), 0, bytes.Length, campo, nombre);
        contexto.Request.ContentType = "multipart/form-data; boundary=x";
        contexto.Request.Form = new Microsoft.AspNetCore.Http.FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
            new Microsoft.AspNetCore.Http.FormFileCollection { fichero });
        return contexto;
    }

    private sealed class EntornoFalso(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ListaLogger : ILogger<LocalFileStorageService>
    {
        public List<string> Avisos { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Avisos.Add(formatter(state, exception));
            }
        }
    }
}
