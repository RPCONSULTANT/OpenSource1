using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.GlobalExceptionHandling;

/// <summary>
/// Prueba directa de <see cref="GlobalExceptionHandler"/>, sin levantar la API completa: no hay
/// Docker disponible en este entorno, así que el camino HTTP de punta a punta (una petición real
/// contra <c>GET /api/productos?precio=abc</c>) queda sin verificar. Esta prueba demuestra que,
/// dada una <see cref="ErroresDeDominioException"/> construida a mano — la misma que ahora lanza
/// <c>DapperProductoReadRepository</c> ante un filtro inválido — el handler responde 400 con un
/// cuerpo que nombra el campo afectado, no 500 y no una página HTML.
/// </summary>
public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ConErroresDeDominioException_Devuelve400ConCampoNombrado()
    {
        var handler = CrearHandler(entornoDesarrollo: false);

        var error = new Error("filtro.valor_invalido", "El valor 'abc' no es válido para el filtro 'Precio'.", "Precio");
        var excepcion = new ErroresDeDominioException(error);

        var httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
        };

        var manejada = await handler.TryHandleAsync(httpContext, excepcion, CancellationToken.None);

        Assert.True(manejada);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.StartsWith("application/problem+json", httpContext.Response.ContentType);

        var cuerpo = await LeerCuerpoAsync(httpContext);
        Assert.Contains("\"Precio\"", cuerpo);
        Assert.Contains("El valor 'abc' no es válido para el filtro 'Precio'.", cuerpo);
        Assert.DoesNotContain("<html", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryHandleAsync_ConVariosErroresDelMismoCampo_LosFusionaEnUnArray()
    {
        var handler = CrearHandler(entornoDesarrollo: false);

        var excepcion = new ErroresDeDominioException(
        [
            new Error("cliente.email_invalido", "El email no tiene un formato válido.", "Email"),
            new Error("cliente.email_duplicado", "Ya existe un cliente con ese email.", "Email"),
        ]);

        var httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
        };

        await handler.TryHandleAsync(httpContext, excepcion, CancellationToken.None);

        var errores = await LeerErroresAsync(httpContext);
        Assert.Single(errores.EnumerateObject());
        var mensajesEmail = errores.GetProperty("Email");
        Assert.Equal(2, mensajesEmail.GetArrayLength());
        Assert.Equal("El email no tiene un formato válido.", mensajesEmail[0].GetString());
        Assert.Equal("Ya existe un cliente con ese email.", mensajesEmail[1].GetString());
    }

    [Fact]
    public async Task TryHandleAsync_ConErroresDeCamposDistintos_UsaClavesSeparadas()
    {
        var handler = CrearHandler(entornoDesarrollo: false);

        var excepcion = new ErroresDeDominioException(
        [
            new Error("cliente.email_invalido", "El email no es válido.", "Email"),
            new Error("cliente.pais_invalido", "El país no es válido.", "PaisCodigo"),
        ]);

        var httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
        };

        await handler.TryHandleAsync(httpContext, excepcion, CancellationToken.None);

        var errores = await LeerErroresAsync(httpContext);
        Assert.Equal(2, errores.EnumerateObject().Count());
        Assert.Single(errores.GetProperty("Email").EnumerateArray());
        Assert.Single(errores.GetProperty("PaisCodigo").EnumerateArray());
    }

    [Fact]
    public async Task TryHandleAsync_ConErrorDeCampoNulo_CaeBajoClaveVaciaSinColisionar()
    {
        var handler = CrearHandler(entornoDesarrollo: false);

        var excepcion = new ErroresDeDominioException(
        [
            new Error("cliente.regla_general", "Regla de negocio general violada.", null),
            new Error("cliente.email_invalido", "El email no es válido.", "Email"),
        ]);

        var httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
        };

        await handler.TryHandleAsync(httpContext, excepcion, CancellationToken.None);

        var errores = await LeerErroresAsync(httpContext);
        Assert.Equal(2, errores.EnumerateObject().Count());
        Assert.True(errores.TryGetProperty("", out var claveVacia));
        var mensajesClaveVacia = Assert.Single(claveVacia.EnumerateArray());
        Assert.Equal("Regla de negocio general violada.", mensajesClaveVacia.GetString());
        Assert.Single(errores.GetProperty("Email").EnumerateArray());
    }

    [Fact]
    public async Task TryHandleAsync_ConExcepcionGenerica_Devuelve500SinDetalleEnProduccion()
    {
        var handler = CrearHandler(entornoDesarrollo: false);

        var httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
        };

        var manejada = await handler.TryHandleAsync(httpContext, new InvalidOperationException("secreto interno"), CancellationToken.None);

        Assert.True(manejada);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);

        var cuerpo = await LeerCuerpoAsync(httpContext);
        Assert.DoesNotContain("secreto interno", cuerpo);
        Assert.Contains("traceId", cuerpo);
    }

    [Fact]
    public async Task TryHandleAsync_ConExcepcionGenerica_IncluyeDetalleEnDevelopment()
    {
        var handler = CrearHandler(entornoDesarrollo: true);

        var httpContext = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() },
        };

        await handler.TryHandleAsync(httpContext, new InvalidOperationException("detalle visible en dev"), CancellationToken.None);

        var cuerpo = await LeerCuerpoAsync(httpContext);
        Assert.Contains("detalle visible en dev", cuerpo);
    }

    private static GlobalExceptionHandler CrearHandler(bool entornoDesarrollo)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();

        var environment = new FakeHostEnvironment
        {
            EnvironmentName = entornoDesarrollo ? Environments.Development : Environments.Production,
        };

        return new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            environment,
            NullLogger<GlobalExceptionHandler>.Instance);
    }

    private static async Task<string> LeerCuerpoAsync(DefaultHttpContext httpContext)
    {
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Parsea el cuerpo y devuelve la propiedad "errors" del ValidationProblemDetails.</summary>
    private static async Task<JsonElement> LeerErroresAsync(DefaultHttpContext httpContext)
    {
        var cuerpo = await LeerCuerpoAsync(httpContext);
        using var documento = JsonDocument.Parse(cuerpo);
        return documento.RootElement.GetProperty("errors").Clone();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "OpenSource1.SmokeTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
