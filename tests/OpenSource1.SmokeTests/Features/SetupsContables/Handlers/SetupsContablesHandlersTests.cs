using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SetupsContables;
using OpenSource1.Application.Features.SetupsContables.Commands;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Application.Features.SetupsContables.Handlers;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.SetupsContables.Handlers;

/// <summary>Handlers de los tres setups contables (Task 5.4) con repositorios en memoria que evalúan los predicados.</summary>
public sealed class SetupsContablesHandlersTests
{
    private sealed class Fake
    {
        public Fake()
        {
            UnitOfWork.Setup(u => u.Repository<SetupContableGeneral>()).Returns(General.Repo);
            UnitOfWork.Setup(u => u.Repository<SetupIva>()).Returns(Iva.Repo);
            UnitOfWork.Setup(u => u.Repository<SetupInventario>()).Returns(Inventario.Repo);
            UnitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(Cuentas.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoNegocio>()).Returns(GruposNegocio.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoProducto>()).Returns(GruposProducto.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoIvaNegocio>()).Returns(GruposIvaNegocio.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoIvaProducto>()).Returns(GruposIvaProducto.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoInventario>()).Returns(GruposInventario.Repo);
            UnitOfWork.Setup(u => u.Repository<Almacen>()).Returns(Almacenes.Repo);
            UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Lectura.Setup(r => r.GetGeneralAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => new SetupGeneralResponse { Id = id });
            Lectura.Setup(r => r.GetIvaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => new SetupIvaResponse { Id = id });
            Lectura.Setup(r => r.GetInventarioAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => new SetupInventarioResponse { Id = id });

            Posteo = Cuenta("4101", TipoCuentaContable.Posteo);
            Otra = Cuenta("5101", TipoCuentaContable.Posteo);
            Encabezado = Cuenta("4", TipoCuentaContable.Encabezado);
            Bloqueada = Cuenta("4199", TipoCuentaContable.Posteo, bloqueada: true);
            Nacional = GruposNegocio.Agregar(new GrupoNegocio { Codigo = "NACIONAL", Descripcion = "N" });
            Bienes = GruposProducto.Agregar(new GrupoProducto { Codigo = "BIENES", Descripcion = "B" });
            IvaNegocio = GruposIvaNegocio.Agregar(new GrupoIvaNegocio { Codigo = "ITBIS18", Descripcion = "I" });
            IvaProducto = GruposIvaProducto.Agregar(new GrupoIvaProducto { Codigo = "ITBIS18", Descripcion = "I" });
            General_ = GruposInventario.Agregar(new GrupoInventario { Codigo = "GENERAL", Descripcion = "G" });
            Principal = Almacenes.Agregar(new Almacen { Codigo = "PRINCIPAL", Nombre = "Principal" });
        }

        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ISetupContableReadRepository> Lectura { get; } = new();
        public RepositorioEnMemoria<SetupContableGeneral> General { get; } = new();
        public RepositorioEnMemoria<SetupIva> Iva { get; } = new();
        public RepositorioEnMemoria<SetupInventario> Inventario { get; } = new();
        public RepositorioEnMemoria<CuentaContable> Cuentas { get; } = new();
        public RepositorioEnMemoria<GrupoNegocio> GruposNegocio { get; } = new();
        public RepositorioEnMemoria<GrupoProducto> GruposProducto { get; } = new();
        public RepositorioEnMemoria<GrupoIvaNegocio> GruposIvaNegocio { get; } = new();
        public RepositorioEnMemoria<GrupoIvaProducto> GruposIvaProducto { get; } = new();
        public RepositorioEnMemoria<GrupoInventario> GruposInventario { get; } = new();
        public RepositorioEnMemoria<Almacen> Almacenes { get; } = new();

        public CuentaContable Posteo { get; }
        public CuentaContable Otra { get; }
        public CuentaContable Encabezado { get; }
        public CuentaContable Bloqueada { get; }
        public GrupoNegocio Nacional { get; }
        public GrupoProducto Bienes { get; }
        public GrupoIvaNegocio IvaNegocio { get; }
        public GrupoIvaProducto IvaProducto { get; }
        public GrupoInventario General_ { get; }
        public Almacen Principal { get; }

        public CuentaContable Cuenta(string numero, TipoCuentaContable tipo, bool bloqueada = false) => Cuentas.Agregar(new CuentaContable
        {
            Numero = numero, Nombre = $"Cuenta {numero}", TipoCuenta = tipo, TipoResultado = TipoResultadoCuenta.Resultado, Bloqueada = bloqueada
        });

        public SetupContableGeneral FilaGeneral(Guid? negocio, Guid? cuenta = null) => General.Agregar(new SetupContableGeneral
        {
            GrupoNegocioId = negocio, GrupoProductoId = Bienes.Id, CuentaVentasId = cuenta ?? Posteo.Id, CuentaCostoVentasId = cuenta ?? Posteo.Id,
            CuentaDescuentoVentasId = cuenta ?? Posteo.Id, CuentaAjusteInventarioId = cuenta ?? Posteo.Id
        });

        public CreateSetupGeneralCommand AltaGeneral(Guid? negocio, Guid? producto = null, Guid? costo = null) =>
            new(negocio, producto ?? Bienes.Id, Posteo.Id, costo ?? Posteo.Id, Posteo.Id, Posteo.Id);

        public CreateSetupIvaCommand AltaIva(decimal porcentaje = 18m, TipoCalculoIva tipo = TipoCalculoIva.Normal, string identificador = "itbis18", Guid? compras = null) =>
            new(IvaNegocio.Id, IvaProducto.Id, porcentaje, Posteo.Id, compras, identificador, tipo);

        public CreateSetupGeneralCommandHandler CrearGeneral() => new(UnitOfWork.Object, Lectura.Object);
        public UpdateSetupGeneralCommandHandler ModificarGeneral() => new(UnitOfWork.Object, Lectura.Object);
        public CreateSetupIvaCommandHandler CrearIva() => new(UnitOfWork.Object, Lectura.Object);
        public UpdateSetupIvaCommandHandler ModificarIva() => new(UnitOfWork.Object, Lectura.Object);
        public CreateSetupInventarioCommandHandler CrearInventario() => new(UnitOfWork.Object, Lectura.Object);
        public DeleteSetupContableCommandHandler Borrar() => new(UnitOfWork.Object);
    }

    private static Error UnicoError(Result result)
    {
        Assert.True(result.EsFallo);
        return Assert.Single(result.Errores);
    }

    // ── General ──

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateGeneral_ConGrupoNegocioNuloOVacio_GuardaElComodin(bool guidVacio)
    {
        var fake = new Fake();

        var result = await fake.CrearGeneral().Handle(fake.AltaGeneral(guidVacio ? Guid.Empty : null), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        var guardado = Assert.Single(fake.General.Datos);
        Assert.Null(guardado.GrupoNegocioId);
        Assert.Equal(fake.Bienes.Id, guardado.GrupoProductoId);
        Assert.Equal(guardado.Id, result.Valor.Id);
    }

    [Fact]
    public async Task CreateGeneral_SinGrupoProducto_Devuelve400GrupoRequerido()
    {
        var fake = new Fake();

        var error = UnicoError(await fake.CrearGeneral().Handle(fake.AltaGeneral(null, producto: Guid.Empty), default));

        Assert.Equal("setup_contable.grupo_requerido", error.Codigo);
        Assert.Equal("GrupoProductoId", error.Campo);
        Assert.Empty(fake.General.Datos);
    }

    [Fact]
    public async Task CreateGeneral_GrupoInexistenteOBorrado_Devuelve400GrupoInvalido_NoNoEncontrado()
    {
        var fake = new Fake();
        var borrado = fake.GruposNegocio.Agregar(new GrupoNegocio { Codigo = "BORRADO", Descripcion = "X", IsDeleted = true });

        var inexistente = UnicoError(await fake.CrearGeneral().Handle(fake.AltaGeneral(Guid.NewGuid()), default));
        var deBorrado = UnicoError(await fake.CrearGeneral().Handle(fake.AltaGeneral(borrado.Id), default));

        Assert.Equal("setup_contable.grupo_invalido", inexistente.Codigo);
        Assert.Equal("GrupoNegocioId", inexistente.Campo);
        Assert.Equal("setup_contable.grupo_invalido", deBorrado.Codigo);
        Assert.Empty(fake.General.Datos);
    }

    [Theory]
    [InlineData("encabezado", "setup_contable.cuenta_invalida")]
    [InlineData("total", "setup_contable.cuenta_invalida")]
    [InlineData("bloqueada", "setup_contable.cuenta_invalida")]
    [InlineData("inexistente", "setup_contable.cuenta_invalida")]
    [InlineData("vacia", "setup_contable.cuenta_requerida")]
    public async Task CreateGeneral_CuentaNoValida_Devuelve400EnSuCampo(string caso, string codigo)
    {
        var fake = new Fake();
        var cuenta = caso switch
        {
            "encabezado" => fake.Encabezado.Id,
            "total" => fake.Cuenta("4999", TipoCuentaContable.Total).Id,
            "bloqueada" => fake.Bloqueada.Id,
            "inexistente" => Guid.NewGuid(),
            _ => Guid.Empty
        };

        var error = UnicoError(await fake.CrearGeneral().Handle(fake.AltaGeneral(null, costo: cuenta), default));

        Assert.Equal(codigo, error.Codigo);
        Assert.Equal("CuentaCostoVentasId", error.Campo);
        Assert.Empty(fake.General.Datos);
    }

    [Fact]
    public async Task CreateGeneral_CombinacionExistente_Devuelve409ConLosCodigos_SalvoQueLaExistenteEsteBorrada()
    {
        var fake = new Fake();
        fake.FilaGeneral(null);
        fake.FilaGeneral(fake.Nacional.Id).IsDeleted = true;

        var comodinDuplicado = UnicoError(await fake.CrearGeneral().Handle(fake.AltaGeneral(null), default));
        var sobreBorrada = await fake.CrearGeneral().Handle(fake.AltaGeneral(fake.Nacional.Id), default);

        Assert.Equal("setup_contable.conflicto", comodinDuplicado.Codigo);
        Assert.Contains("GrupoNegocio=cualquiera × GrupoProducto=BIENES", comodinDuplicado.Mensaje);
        Assert.True(sobreBorrada.EsExito);
    }

    [Fact]
    public async Task UpdateGeneral_Inexistente_Devuelve404Code()
    {
        var fake = new Fake();

        var error = UnicoError(await fake.ModificarGeneral().Handle(
            new UpdateSetupGeneralCommand(Guid.NewGuid(), null, fake.Bienes.Id, fake.Posteo.Id, fake.Posteo.Id, fake.Posteo.Id, fake.Posteo.Id, 1), default));

        Assert.Equal("setup_contable.no_encontrado", error.Codigo);
    }

    [Fact]
    public async Task UpdateGeneral_CuentaQueNoCambiaNoSeRevalida_UnaNuevaBloqueadaSeRechaza_YFijaXmin()
    {
        var fake = new Fake();
        var fila = fake.FilaGeneral(null, cuenta: fake.Bloqueada.Id);

        // La cuenta bloqueada ya estaba: se conserva sin revalidar; la de costo cambia a una válida.
        var ok = await fake.ModificarGeneral().Handle(new UpdateSetupGeneralCommand(
            fila.Id, fake.Nacional.Id, fake.Bienes.Id, fake.Bloqueada.Id, fake.Otra.Id, fake.Bloqueada.Id, fake.Bloqueada.Id, 42), default);
        Assert.True(ok.EsExito, ok.EsFallo ? ok.Errores[0].Mensaje : null);
        Assert.Equal(fake.Nacional.Id, fila.GrupoNegocioId);
        Assert.Equal(fake.Otra.Id, fila.CuentaCostoVentasId);
        fake.General.Mock.Verify(r => r.EstablecerVersionOriginal(fila, 42), Times.Once);

        // Cambiar a una cuenta bloqueada distinta de la vigente sí se rechaza, y no toca la fila.
        var otraBloqueada = fake.Cuenta("4198", TipoCuentaContable.Posteo, bloqueada: true);
        var error = UnicoError(await fake.ModificarGeneral().Handle(new UpdateSetupGeneralCommand(
            fila.Id, fake.Nacional.Id, fake.Bienes.Id, fake.Bloqueada.Id, otraBloqueada.Id, fake.Bloqueada.Id, fake.Bloqueada.Id, 43), default));
        Assert.Equal("setup_contable.cuenta_invalida", error.Codigo);
        Assert.Equal("CuentaCostoVentasId", error.Campo);
        Assert.Equal(fake.Otra.Id, fila.CuentaCostoVentasId);
        fake.General.Mock.Verify(r => r.EstablecerVersionOriginal(fila, 43), Times.Never);
    }

    [Fact]
    public async Task UpdateGeneral_ASuPropiaCombinacionNoChoca_PeroALaDeOtraFilaDa409()
    {
        var fake = new Fake();
        var comodin = fake.FilaGeneral(null);
        var nacional = fake.FilaGeneral(fake.Nacional.Id);

        var mismaCombinacion = await fake.ModificarGeneral().Handle(new UpdateSetupGeneralCommand(
            nacional.Id, fake.Nacional.Id, fake.Bienes.Id, fake.Otra.Id, fake.Posteo.Id, fake.Posteo.Id, fake.Posteo.Id, 1), default);
        var aComodin = UnicoError(await fake.ModificarGeneral().Handle(new UpdateSetupGeneralCommand(
            nacional.Id, null, fake.Bienes.Id, fake.Posteo.Id, fake.Posteo.Id, fake.Posteo.Id, fake.Posteo.Id, 2), default));

        Assert.True(mismaCombinacion.EsExito);
        Assert.Equal("setup_contable.conflicto", aComodin.Codigo);
        Assert.Equal(fake.Nacional.Id, nacional.GrupoNegocioId);
        Assert.NotEqual(comodin.Id, nacional.Id);
    }

    // ── IVA ──

    [Theory]
    [InlineData("-1", TipoCalculoIva.Normal)]
    [InlineData("100.00001", TipoCalculoIva.Normal)]
    [InlineData("12.123456", TipoCalculoIva.Normal)]
    [InlineData("18", TipoCalculoIva.Exento)]
    public async Task CreateIva_PorcentajeFueraDeRango_ConMasDe5Decimales_OExentoDistintoDeCero_Devuelve400(string porcentaje, TipoCalculoIva tipo)
    {
        var fake = new Fake();

        var error = UnicoError(await fake.CrearIva().Handle(fake.AltaIva(decimal.Parse(porcentaje, System.Globalization.CultureInfo.InvariantCulture), tipo), default));

        Assert.Equal("setup_contable.porcentaje_invalido", error.Codigo);
        Assert.Equal("PorcentajeIva", error.Campo);
        Assert.Empty(fake.Iva.Datos);
    }

    [Fact]
    public async Task CreateIva_Valido_NormalizaElIdentificador_YAdmite5Decimales_YExentoConCero()
    {
        var fake = new Fake();

        var normal = await fake.CrearIva().Handle(fake.AltaIva(12.34567m, identificador: " itbis18 "), default);
        Assert.True(normal.EsExito, normal.EsFallo ? normal.Errores[0].Mensaje : null);
        var guardado = Assert.Single(fake.Iva.Datos);
        Assert.Equal("ITBIS18", guardado.IdentificadorIva);
        Assert.Equal(12.34567m, guardado.PorcentajeIva);
        Assert.Null(guardado.CuentaIvaComprasId);

        var otroProducto = fake.GruposIvaProducto.Agregar(new GrupoIvaProducto { Codigo = "EXENTO", Descripcion = "E" });
        var exento = await fake.CrearIva().Handle(
            new CreateSetupIvaCommand(null, otroProducto.Id, 0m, fake.Posteo.Id, null, "EXENTO", TipoCalculoIva.Exento), default);
        Assert.True(exento.EsExito, exento.EsFallo ? exento.Errores[0].Mensaje : null);
    }

    [Theory]
    [InlineData("", "setup_contable.identificador_requerido")]
    [InlineData("con espacio", "setup_contable.identificador_invalido")]
    public async Task CreateIva_IdentificadorNoValido_Devuelve400(string identificador, string codigo)
    {
        var fake = new Fake();

        var error = UnicoError(await fake.CrearIva().Handle(fake.AltaIva(identificador: identificador), default));

        Assert.Equal(codigo, error.Codigo);
        Assert.Equal("IdentificadorIva", error.Campo);
    }

    [Fact]
    public async Task CreateIva_TipoDeCalculoDesconocido_Devuelve400()
    {
        var fake = new Fake();

        var error = UnicoError(await fake.CrearIva().Handle(fake.AltaIva(tipo: (TipoCalculoIva)99), default));

        Assert.Equal("setup_contable.tipo_calculo_invalido", error.Codigo);
    }

    [Fact]
    public async Task UpdateIva_CuentaComprasNullConserva_GuidVacioQuita_YUnaNuevaDeEncabezadoSeRechaza()
    {
        var fake = new Fake();
        var fila = fake.Iva.Agregar(new SetupIva
        {
            GrupoIvaNegocioId = fake.IvaNegocio.Id, GrupoIvaProductoId = fake.IvaProducto.Id, PorcentajeIva = 18m, CuentaIvaVentasId = fake.Posteo.Id,
            CuentaIvaComprasId = fake.Otra.Id, IdentificadorIva = "ITBIS18", TipoCalculoIva = TipoCalculoIva.Normal
        });
        UpdateSetupIvaCommand Comando(Guid? compras, long xmin) =>
            new(fila.Id, fake.IvaNegocio.Id, fake.IvaProducto.Id, 16m, fake.Posteo.Id, compras, "IVA16", TipoCalculoIva.Normal, xmin);

        Assert.True((await fake.ModificarIva().Handle(Comando(null, 1), default)).EsExito);
        Assert.Equal(fake.Otra.Id, fila.CuentaIvaComprasId);
        Assert.Equal(16m, fila.PorcentajeIva);
        Assert.Equal("IVA16", fila.IdentificadorIva);

        var error = UnicoError(await fake.ModificarIva().Handle(Comando(fake.Encabezado.Id, 2), default));
        Assert.Equal("setup_contable.cuenta_invalida", error.Codigo);
        Assert.Equal("CuentaIvaComprasId", error.Campo);

        Assert.True((await fake.ModificarIva().Handle(Comando(Guid.Empty, 3), default)).EsExito);
        Assert.Null(fila.CuentaIvaComprasId);
    }

    // ── Inventario ──

    [Fact]
    public async Task CreateInventario_ComodinDeAlmacen_Guarda_AlmacenInexistente400_YDuplicado409()
    {
        var fake = new Fake();
        CreateSetupInventarioCommand Alta(Guid? almacen) => new(almacen, fake.General_.Id, fake.Posteo.Id, fake.Posteo.Id, fake.Posteo.Id);

        Assert.True((await fake.CrearInventario().Handle(Alta(null), default)).EsExito);
        Assert.True((await fake.CrearInventario().Handle(Alta(fake.Principal.Id), default)).EsExito);
        Assert.Null(fake.Inventario.Datos[0].AlmacenId);

        var inexistente = UnicoError(await fake.CrearInventario().Handle(Alta(Guid.NewGuid()), default));
        Assert.Equal("setup_contable.grupo_invalido", inexistente.Codigo);
        Assert.Equal("AlmacenId", inexistente.Campo);

        var duplicado = UnicoError(await fake.CrearInventario().Handle(Alta(fake.Principal.Id), default));
        Assert.Equal("setup_contable.conflicto", duplicado.Codigo);
        Assert.Contains("Almacen=PRINCIPAL × GrupoInventario=GENERAL", duplicado.Mensaje);
        Assert.Equal(2, fake.Inventario.Datos.Count);
    }

    // ── Borrado ──

    [Fact]
    public async Task Delete_Existente_LoBorra_EInexistenteDevuelveNoEncontrado()
    {
        var fake = new Fake();
        var fila = fake.FilaGeneral(null);

        Assert.True((await fake.Borrar().Handle(new DeleteSetupContableCommand(TipoSetupContable.General, fila.Id), default)).EsExito);
        fake.General.Mock.Verify(r => r.Remove(fila), Times.Once);

        // El mismo Id bajo otro tipo es otra tabla: no existe.
        var error = UnicoError(await fake.Borrar().Handle(new DeleteSetupContableCommand(TipoSetupContable.Iva, fila.Id), default));
        Assert.Equal("setup_contable.no_encontrado", error.Codigo);
    }
}
