using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// <see cref="IDerivadorCuentas"/> contra Postgres real (Task 5.4): semillas, fila exacta frente a comodín (Review Focus 5),
/// combinación inexistente con los CÓDIGOS en el mensaje, grupo principal nulo, cuenta que deja de ser válida, filas borradas,
/// lectura dentro de la transacción del llamador y la unicidad <c>NULLS NOT DISTINCT</c>. Cada test crea sus propios grupos y
/// cuentas (datos aislados por Guid). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DerivadorCuentasTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    // ── Semillas ──

    [Fact]
    public async Task Semillas_General_NacionalBienesExacta_YExteriorServiciosPorComodin()
    {
        Assert.Equal(CuentaContableIds.Ventas, Ok(await DerivarAsync(d => d.CuentaVentasAsync(GrupoContableIds.NegocioNacional, GrupoContableIds.ProductoBienes))));
        Assert.Equal(CuentaContableIds.CostoVentas, Ok(await DerivarAsync(d => d.CuentaCostoVentasAsync(GrupoContableIds.NegocioNacional, GrupoContableIds.ProductoBienes))));
        Assert.Equal(CuentaContableIds.DescuentoVentas, Ok(await DerivarAsync(d => d.CuentaDescuentoVentasAsync(GrupoContableIds.NegocioNacional, GrupoContableIds.ProductoServicios))));
        // EXTERIOR no tiene fila propia: cae en el comodín NULL × SERVICIOS.
        Assert.Equal(CuentaContableIds.Ventas, Ok(await DerivarAsync(d => d.CuentaVentasAsync(GrupoContableIds.NegocioExterior, GrupoContableIds.ProductoServicios))));
        // Sin grupo de negocio (socio sin clasificar): también el comodín.
        Assert.Equal(CuentaContableIds.CostoVentas, Ok(await DerivarAsync(d => d.CuentaCostoVentasAsync(null, GrupoContableIds.ProductoBienes))));
    }

    [Fact]
    public async Task Semillas_Iva_Itbis18YExentos()
    {
        var itbis = Ok(await DerivarAsync(d => d.IvaAsync(GrupoContableIds.IvaNegocioItbis18, GrupoContableIds.IvaProductoItbis18)));
        Assert.Equal(new SetupIvaResuelto(18m, CuentaContableIds.IvaPorPagar, null, "ITBIS18", TipoCalculoIva.Normal), itbis);

        foreach (var (negocio, producto) in new[]
                 {
                     (GrupoContableIds.IvaNegocioItbis18, GrupoContableIds.IvaProductoExento),
                     (GrupoContableIds.IvaNegocioExento, GrupoContableIds.IvaProductoItbis18),
                     (GrupoContableIds.IvaNegocioExento, GrupoContableIds.IvaProductoExento),
                 })
        {
            var exento = Ok(await DerivarAsync(d => d.IvaAsync(negocio, producto)));
            Assert.Equal(new SetupIvaResuelto(0m, CuentaContableIds.IvaPorPagar, null, "EXENTO", TipoCalculoIva.Exento), exento);
        }
    }

    [Fact]
    public async Task Semillas_InventarioPorComodinDeAlmacen_YCxCDelGrupoGeneral()
    {
        Assert.Equal(CuentaContableIds.Inventario, Ok(await DerivarAsync(d => d.CuentaInventarioAsync(AlmacenIds.Principal, GrupoContableIds.InventarioGeneral))));
        Assert.Equal(CuentaContableIds.AjusteInventario, Ok(await DerivarAsync(d => d.CuentaAjusteInventarioAsync(AlmacenIds.Principal, GrupoContableIds.InventarioGeneral))));
        Assert.Equal(CuentaContableIds.Inventario, Ok(await DerivarAsync(d => d.CuentaInventarioAsync(null, GrupoContableIds.InventarioGeneral))));
        Assert.Equal(CuentaContableIds.CxC, Ok(await DerivarAsync(d => d.CuentaCxCAsync(GrupoContableIds.ClienteContableGeneral))));
    }

    // ── Exacta frente a comodín (Review Focus 5) ──

    [Fact]
    public async Task General_FilaExactaGanaAlComodin()
    {
        var n1 = await GrupoAsync<GrupoNegocio>();
        var n2 = await GrupoAsync<GrupoNegocio>();
        var p1 = await GrupoAsync<GrupoProducto>();
        var exacta = await CuentaAsync();
        var comodin = await CuentaAsync();
        // El comodín se inserta PRIMERO: el orden físico no debe decidir.
        await SetupGeneralAsync(null, p1, comodin);
        await SetupGeneralAsync(n1, p1, exacta);

        Assert.Equal(exacta, Ok(await DerivarAsync(d => d.CuentaVentasAsync(n1, p1))));
        Assert.Equal(comodin, Ok(await DerivarAsync(d => d.CuentaVentasAsync(n2, p1))));
        Assert.Equal(comodin, Ok(await DerivarAsync(d => d.CuentaVentasAsync(null, p1))));
    }

    [Fact]
    public async Task General_ElComodinDeUnEjeNoSeConfundeConElOtroEje()
    {
        var n1 = await GrupoAsync<GrupoNegocio>();
        var n2 = await GrupoAsync<GrupoNegocio>();
        var p1 = await GrupoAsync<GrupoProducto>();
        var p2 = await GrupoAsync<GrupoProducto>();
        var a = await CuentaAsync();
        var b = await CuentaAsync();
        await SetupGeneralAsync(n1, p1, a);
        await SetupGeneralAsync(null, p2, b);

        // (n1, p2): no hay exacta; el comodín de p2 aplica.
        Assert.Equal(b, Ok(await DerivarAsync(d => d.CuentaVentasAsync(n1, p2))));
        // (n2, p1): la fila de n1 no es un comodín de negocio, y el comodín de p2 no sirve para p1.
        var sinSetup = await DerivarAsync(d => d.CuentaVentasAsync(n2, p1));
        var error = Assert.Single(sinSetup.Errores);
        Assert.Equal("setup_contable.inexistente", error.Codigo);
        Assert.Equal(
            $"No existe setup SetupsContableGeneral para GrupoProducto={await CodigoAsync<GrupoProducto>(p1)} × GrupoNegocio={await CodigoAsync<GrupoNegocio>(n2)}",
            error.Mensaje);
        Assert.DoesNotContain(n2.ToString(), error.Mensaje);
        Assert.DoesNotContain(p1.ToString(), error.Mensaje);
    }

    [Fact]
    public async Task Iva_FilaExactaGanaAlComodin_YElComodinSoloEsDelEjeDeNegocio()
    {
        var n1 = await GrupoAsync<GrupoIvaNegocio>();
        var p1 = await GrupoAsync<GrupoIvaProducto>();
        var p2 = await GrupoAsync<GrupoIvaProducto>();
        var cuenta = await CuentaAsync();
        await SetupIvaAsync(null, p1, 16m, cuenta, "IVA16");
        await SetupIvaAsync(n1, p1, 18m, cuenta, "IVA18");

        Assert.Equal(18m, Ok(await DerivarAsync(d => d.IvaAsync(n1, p1))).PorcentajeIva);
        Assert.Equal("IVA16", Ok(await DerivarAsync(d => d.IvaAsync(null, p1))).IdentificadorIva);
        // La fila exacta de n1 no es un comodín de producto.
        var error = Assert.Single((await DerivarAsync(d => d.IvaAsync(n1, p2))).Errores);
        Assert.Equal("setup_contable.inexistente", error.Codigo);
        Assert.Contains($"GrupoIvaProducto={await CodigoAsync<GrupoIvaProducto>(p2)}", error.Mensaje);
    }

    [Fact]
    public async Task Inventario_AlmacenExactoGanaAlComodin_YOtroAlmacenCaeEnElComodin()
    {
        var principal = await _prueba.SembrarAlmacenAsync();
        var otro = await _prueba.SembrarAlmacenAsync();
        var grupo = await GrupoAsync<GrupoInventario>();
        var exacta = await CuentaAsync();
        var comodin = await CuentaAsync();
        await SetupInventarioAsync(null, grupo, comodin);
        await SetupInventarioAsync(principal, grupo, exacta);

        Assert.Equal(exacta, Ok(await DerivarAsync(d => d.CuentaInventarioAsync(principal, grupo))));
        Assert.Equal(comodin, Ok(await DerivarAsync(d => d.CuentaInventarioAsync(otro, grupo))));
        Assert.Equal(comodin, Ok(await DerivarAsync(d => d.CuentaAjusteInventarioAsync(otro, grupo))));
    }

    // ── Sin setup ──

    [Fact]
    public async Task SinSetup_DevuelveInexistenteConLosCodigos_YCualquieraParaElEjeNulo()
    {
        var p = await GrupoAsync<GrupoProducto>();
        var inv = await GrupoAsync<GrupoInventario>();
        var almacen = await _prueba.SembrarAlmacenAsync();

        var general = Assert.Single((await DerivarAsync(d => d.CuentaVentasAsync(null, p))).Errores);
        Assert.Equal("setup_contable.inexistente", general.Codigo);
        Assert.Equal($"No existe setup SetupsContableGeneral para GrupoProducto={await CodigoAsync<GrupoProducto>(p)} × GrupoNegocio=cualquiera", general.Mensaje);

        var inventario = Assert.Single((await DerivarAsync(d => d.CuentaInventarioAsync(almacen, inv))).Errores);
        Assert.Equal("setup_contable.inexistente", inventario.Codigo);
        await using var contexto = _prueba.NuevoContexto();
        var codigoAlmacen = await contexto.Almacenes.Where(a => a.Id == almacen).Select(a => a.Codigo).SingleAsync();
        Assert.Equal($"No existe setup SetupsInventario para GrupoInventario={await CodigoAsync<GrupoInventario>(inv)} × Almacen={codigoAlmacen}", inventario.Mensaje);

        // Un grupo de cliente inexistente tampoco tiene "setup".
        Assert.Equal("setup_contable.inexistente", Assert.Single((await DerivarAsync(d => d.CuentaCxCAsync(Guid.NewGuid()))).Errores).Codigo);
    }

    [Fact]
    public async Task GrupoPrincipalNulo_DevuelveGrupoFaltante_EnCadaLookup()
    {
        await AssertGrupoFaltanteAsync(d => d.CuentaVentasAsync(GrupoContableIds.NegocioNacional, null), "grupo de producto");
        await AssertGrupoFaltanteAsync(d => d.CuentaCostoVentasAsync(null, null), "grupo de producto");
        await AssertGrupoFaltanteAsync(d => d.CuentaDescuentoVentasAsync(null, null), "grupo de producto");
        await AssertGrupoFaltanteAsync(async d => (await d.IvaAsync(GrupoContableIds.IvaNegocioItbis18, null)), "grupo de IVA de producto");
        await AssertGrupoFaltanteAsync(d => d.CuentaInventarioAsync(AlmacenIds.Principal, null), "grupo de inventario");
        await AssertGrupoFaltanteAsync(d => d.CuentaAjusteInventarioAsync(null, null), "grupo de inventario");
        await AssertGrupoFaltanteAsync(d => d.CuentaCxCAsync(null), "grupo contable de cliente");
    }

    // ── Cuenta inválida ──

    [Fact]
    public async Task CuentaQueDespuesSeBloquea_OSeBorra_ODejaDeSerDePosteo_DevuelveCuentaInvalida()
    {
        var p = await GrupoAsync<GrupoProducto>();
        var cuenta = await CuentaAsync();
        await SetupGeneralAsync(null, p, cuenta);
        Assert.Equal(cuenta, Ok(await DerivarAsync(d => d.CuentaVentasAsync(null, p))));

        await ActualizarCuentaAsync(cuenta, c => c.Bloqueada = true);
        var bloqueada = Assert.Single((await DerivarAsync(d => d.CuentaVentasAsync(null, p))).Errores);
        Assert.Equal("setup_contable.cuenta_invalida", bloqueada.Codigo);
        Assert.Contains("bloqueada", bloqueada.Mensaje);

        await ActualizarCuentaAsync(cuenta, c => { c.Bloqueada = false; c.TipoCuenta = TipoCuentaContable.Encabezado; });
        Assert.Equal("setup_contable.cuenta_invalida", Assert.Single((await DerivarAsync(d => d.CuentaVentasAsync(null, p))).Errores).Codigo);

        await ActualizarCuentaAsync(cuenta, c => { c.TipoCuenta = TipoCuentaContable.Posteo; c.IsDeleted = true; });
        Assert.Equal("setup_contable.cuenta_invalida", Assert.Single((await DerivarAsync(d => d.CuentaVentasAsync(null, p))).Errores).Codigo);
    }

    [Fact]
    public async Task CuentaIvaVentasBloqueada_DevuelveCuentaInvalida_YLaDeCxCTambien()
    {
        var p = await GrupoAsync<GrupoIvaProducto>();
        var cuenta = await CuentaAsync();
        await SetupIvaAsync(null, p, 18m, cuenta, "X18");
        await ActualizarCuentaAsync(cuenta, c => c.Bloqueada = true);
        Assert.Equal("setup_contable.cuenta_invalida", Assert.Single((await DerivarAsync(d => d.IvaAsync(null, p))).Errores).Codigo);

        var cxc = await CuentaAsync();
        var grupoCliente = Guid.NewGuid();
        await using (var contexto = _prueba.NuevoContexto())
        {
            contexto.GruposClienteContable.Add(new GrupoClienteContable { Codigo = NuevoCodigo(), Descripcion = "Prueba", CuentaCxCId = cxc, CreatedBy = "test" });
            await contexto.SaveChangesAsync();
            grupoCliente = contexto.GruposClienteContable.Local.Single().Id;
        }

        Assert.Equal(cxc, Ok(await DerivarAsync(d => d.CuentaCxCAsync(grupoCliente))));
        await ActualizarCuentaAsync(cxc, c => c.Bloqueada = true);
        Assert.Equal("setup_contable.cuenta_invalida", Assert.Single((await DerivarAsync(d => d.CuentaCxCAsync(grupoCliente))).Errores).Codigo);
    }

    // ── Filas borradas y transacción del llamador ──

    [Fact]
    public async Task FilaBorradaLogicamente_SeIgnora_YCaeEnElComodinOEnInexistente()
    {
        var n = await GrupoAsync<GrupoNegocio>();
        var p = await GrupoAsync<GrupoProducto>();
        var exacta = await CuentaAsync();
        var comodin = await CuentaAsync();
        var filaExacta = await SetupGeneralAsync(n, p, exacta);
        var filaComodin = await SetupGeneralAsync(null, p, comodin);

        await BorrarSetupAsync(filaExacta);
        Assert.Equal(comodin, Ok(await DerivarAsync(d => d.CuentaVentasAsync(n, p))));

        await BorrarSetupAsync(filaComodin);
        Assert.Equal("setup_contable.inexistente", Assert.Single((await DerivarAsync(d => d.CuentaVentasAsync(n, p))).Errores).Codigo);
    }

    [Fact]
    public async Task LeeDentroDeLaTransaccionDelLlamador_YNoEscribeNada()
    {
        var p = await GrupoAsync<GrupoProducto>();
        var cuenta = await CuentaAsync();

        await using (var scope = _prueba.Provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var derivador = scope.ServiceProvider.GetRequiredService<IDerivadorCuentas>();
            await using var transaccion = await unitOfWork.BeginTransactionAsync();
            await unitOfWork.Repository<SetupContableGeneral>().AddAsync(NuevoSetupGeneral(null, p, cuenta));
            await unitOfWork.SaveChangesAsync();

            // La fila aún no confirmada es visible para el derivador porque comparte la transacción.
            Assert.Equal(cuenta, Ok(await derivador.CuentaVentasAsync(null, p)));
            await unitOfWork.RollbackAsync();
        }

        // Tras el rollback no existe: el derivador no escribió nada por su cuenta.
        Assert.Equal("setup_contable.inexistente", Assert.Single((await DerivarAsync(d => d.CuentaVentasAsync(null, p))).Errores).Codigo);
        await using var contexto = _prueba.NuevoContexto();
        Assert.False(await contexto.SetupsContableGeneral.IgnoreQueryFilters().AnyAsync(s => s.GrupoProductoId == p));
    }

    // ── Unicidad NULLS NOT DISTINCT ──

    [Fact]
    public async Task DosFilasComodinConElMismoGrupo_ChocanEnElIndiceUnico_SalvoQueUnaEsteBorrada()
    {
        var p = await GrupoAsync<GrupoProducto>();
        var inv = await GrupoAsync<GrupoInventario>();
        var cuenta = await CuentaAsync();
        var primera = await SetupGeneralAsync(null, p, cuenta);

        var duplicada = await Assert.ThrowsAsync<DbUpdateException>(() => SetupGeneralAsync(null, p, cuenta));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicada.InnerException).SqlState);

        await SetupInventarioAsync(null, inv, cuenta);
        var duplicadaInventario = await Assert.ThrowsAsync<DbUpdateException>(() => SetupInventarioAsync(null, inv, cuenta));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicadaInventario.InnerException).SqlState);

        var ivaProducto = await GrupoAsync<GrupoIvaProducto>();
        await SetupIvaAsync(null, ivaProducto, 18m, cuenta, "X");
        var duplicadaIva = await Assert.ThrowsAsync<DbUpdateException>(() => SetupIvaAsync(null, ivaProducto, 16m, cuenta, "Y"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicadaIva.InnerException).SqlState);

        // El índice es parcial ("IsDeleted" = false): borrada la primera, la combinación se puede volver a crear.
        await BorrarSetupAsync(primera);
        await SetupGeneralAsync(null, p, cuenta);
    }

    // ── Ayudas ──

    private static int _contador = (int)(DateTime.UtcNow.Ticks % 100_000);

    private static string NuevoCodigo() => $"D{Interlocked.Increment(ref _contador)}";

    private static T Ok<T>(Result<T> resultado)
    {
        Assert.True(resultado.EsExito, resultado.EsFallo ? $"{resultado.Errores[0].Codigo}: {resultado.Errores[0].Mensaje}" : null);
        return resultado.Valor;
    }

    private async Task<Result<T>> DerivarAsync<T>(Func<IDerivadorCuentas, Task<Result<T>>> accion)
    {
        await using var scope = _prueba.Provider.CreateAsyncScope();
        return await accion(scope.ServiceProvider.GetRequiredService<IDerivadorCuentas>());
    }

    private async Task AssertGrupoFaltanteAsync<T>(Func<IDerivadorCuentas, Task<Result<T>>> accion, string nombreGrupo)
    {
        var error = Assert.Single((await DerivarAsync(accion)).Errores);
        Assert.Equal("setup_contable.grupo_faltante", error.Codigo);
        Assert.Contains(nombreGrupo, error.Mensaje);
    }

    private async Task<Guid> GrupoAsync<TGrupo>() where TGrupo : GrupoContable, new()
    {
        await using var contexto = _prueba.NuevoContexto();
        var grupo = new TGrupo { Codigo = NuevoCodigo(), Descripcion = "Grupo de prueba del derivador", CreatedBy = "test" };
        contexto.Set<TGrupo>().Add(grupo);
        await contexto.SaveChangesAsync();
        return grupo.Id;
    }

    private async Task<string> CodigoAsync<TGrupo>(Guid id) where TGrupo : GrupoContable
    {
        await using var contexto = _prueba.NuevoContexto();
        return await contexto.Set<TGrupo>().Where(g => g.Id == id).Select(g => g.Codigo).SingleAsync();
    }

    private async Task<Guid> CuentaAsync()
    {
        await using var contexto = _prueba.NuevoContexto();
        var cuenta = new CuentaContable
        {
            Numero = $"9{Interlocked.Increment(ref _contador)}", Nombre = "Cuenta del derivador", TipoCuenta = TipoCuentaContable.Posteo,
            TipoResultado = TipoResultadoCuenta.Resultado, CreatedBy = "test"
        };
        contexto.CuentasContables.Add(cuenta);
        await contexto.SaveChangesAsync();
        return cuenta.Id;
    }

    private async Task ActualizarCuentaAsync(Guid id, Action<CuentaContable> cambio)
    {
        await using var contexto = _prueba.NuevoContexto();
        var cuenta = await contexto.CuentasContables.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        cambio(cuenta);
        await contexto.SaveChangesAsync();
    }

    private static SetupContableGeneral NuevoSetupGeneral(Guid? negocio, Guid producto, Guid cuenta) => new()
    {
        GrupoNegocioId = negocio, GrupoProductoId = producto, CuentaVentasId = cuenta, CuentaCostoVentasId = cuenta,
        CuentaDescuentoVentasId = cuenta, CuentaAjusteInventarioId = cuenta, CreatedBy = "test"
    };

    private async Task<Guid> SetupGeneralAsync(Guid? negocio, Guid producto, Guid cuenta)
    {
        await using var contexto = _prueba.NuevoContexto();
        var setup = NuevoSetupGeneral(negocio, producto, cuenta);
        contexto.SetupsContableGeneral.Add(setup);
        await contexto.SaveChangesAsync();
        return setup.Id;
    }

    private async Task SetupIvaAsync(Guid? negocio, Guid producto, decimal porcentaje, Guid cuenta, string identificador)
    {
        await using var contexto = _prueba.NuevoContexto();
        contexto.SetupsIva.Add(new SetupIva
        {
            GrupoIvaNegocioId = negocio, GrupoIvaProductoId = producto, PorcentajeIva = porcentaje, CuentaIvaVentasId = cuenta,
            IdentificadorIva = identificador, TipoCalculoIva = TipoCalculoIva.Normal, CreatedBy = "test"
        });
        await contexto.SaveChangesAsync();
    }

    private async Task SetupInventarioAsync(Guid? almacen, Guid grupo, Guid cuenta)
    {
        await using var contexto = _prueba.NuevoContexto();
        contexto.SetupsInventario.Add(new SetupInventario
        {
            AlmacenId = almacen, GrupoInventarioId = grupo, CuentaInventarioId = cuenta, CuentaAjusteInventarioId = cuenta,
            CuentaVariacionCostoId = cuenta, CreatedBy = "test"
        });
        await contexto.SaveChangesAsync();
    }

    private async Task BorrarSetupAsync(Guid id)
    {
        await using var contexto = _prueba.NuevoContexto();
        var setup = await contexto.SetupsContableGeneral.SingleAsync(s => s.Id == id);
        setup.IsDeleted = true;
        await contexto.SaveChangesAsync();
    }
}
