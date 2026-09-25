using Microsoft.EntityFrameworkCore;
using OpenSource1.Infrastructure.Data;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Red permanente para el defecto de la Task 1.5c: fuerza la construcción real del modelo de
/// EF Core sin abrir conexión ni requerir Docker. Si un value object usado en un
/// <c>ComplexProperty</c> vuelve a tener un constructor con parámetros que EF no puede enlazar,
/// este test falla en la misma ronda en que se introduce el defecto, en vez de descubrirse solo
/// cuando la API intenta arrancar contra una base de datos real.
/// </summary>
public sealed class ApplicationDbContextModelTests
{
    [Fact]
    public void ElModeloDeApplicationDbContext_SeConstruyeSinErrores()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=validacion_de_modelo_solamente")
            .Options;

        using var contexto = new ApplicationDbContext(options);

        // Fuerza la construcción del modelo sin abrir conexión.
        var modelo = contexto.Model;

        Assert.NotNull(modelo);
        Assert.NotNull(modelo.FindEntityType(typeof(OpenSource1.Core.Entities.Cliente)));
        Assert.NotNull(modelo.FindEntityType(typeof(OpenSource1.Core.Entities.Producto)));
    }
}
