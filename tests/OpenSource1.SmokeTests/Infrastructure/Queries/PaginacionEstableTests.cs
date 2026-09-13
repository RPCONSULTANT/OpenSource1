using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Api;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.AppSettings;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure.Queries;

/// <summary>
/// Hallazgo 2: <c>UnitOfWork.ApplyAuditValues</c> calcula <c>DateTimeOffset.UtcNow</c> UNA VEZ por
/// <c>SaveChangesAsync</c>, así que un insert por lotes produce el mismo <c>CreatedAtUtc</c> en
/// todas las filas del lote. PostgreSQL no garantiza orden estable entre empates de
/// <c>ORDER BY</c>, así que paginar (<c>LIMIT</c>/<c>OFFSET</c>) sobre esa columna puede duplicar
/// u omitir filas entre páginas consecutivas cuando hay empates — el defecto que corrompía en
/// silencio <c>ListAllAsync()</c> (usado por exports, reportería y dashboards). Esta prueba
/// reproduce el empate a propósito (7 filas en un único <c>SaveChangesAsync</c>) y pagina con un
/// tamaño de página pequeño para forzar el peor caso.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PaginacionEstableTests : IClassFixture<PostgresTestFixture>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PaginacionEstableTests(PostgresTestFixture fixture)
    {
        _factory = fixture.CreateFactory();
    }

    [Fact]
    public async Task ListAsync_ConEmpatesDeCreatedAtUtc_NoDuplicaNiOmiteFilasAlPaginar()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repository = unitOfWork.Repository<AppSetting>();

        var prefijo = $"pag-estable-{Guid.NewGuid():N}-";
        var claves = Enumerable.Range(0, 7).Select(i => $"{prefijo}{i:D2}").ToList();

        foreach (var clave in claves)
        {
            await repository.AddAsync(new AppSetting { Key = clave, Value = "v" }, CancellationToken.None);
        }

        // Un único SaveChangesAsync: ApplyAuditValues calcula "now" una sola vez para las 7
        // filas, garantizando el empate de CreatedAtUtc que dispara el defecto sin el fix.
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        var readRepository = scope.ServiceProvider.GetRequiredService<IAppSettingReadRepository>();

        var vistas = new List<string>();
        var pagina = 1;
        const int tamanoPagina = 2;
        while (true)
        {
            var resultado = await readRepository.ListAsync(
                new PageRequest(pagina, tamanoPagina, "CreatedAtUtc", true), CancellationToken.None);
            Assert.True(resultado.EsExito);

            var paginaResultado = resultado.Valor!;
            vistas.AddRange(paginaResultado.Items.Where(x => x.Key.StartsWith(prefijo, StringComparison.Ordinal)).Select(x => x.Key));

            if ((long)pagina * tamanoPagina >= paginaResultado.Total)
            {
                break;
            }

            pagina++;
        }

        // Ni duplicados ni omisiones: el conjunto recorrido en todas las páginas debe ser
        // exactamente el conjunto insertado, cada clave exactamente una vez.
        Assert.Equal(claves.Count, vistas.Count);
        Assert.Equal(claves.OrderBy(k => k, StringComparer.Ordinal), vistas.OrderBy(k => k, StringComparer.Ordinal));
    }
}
