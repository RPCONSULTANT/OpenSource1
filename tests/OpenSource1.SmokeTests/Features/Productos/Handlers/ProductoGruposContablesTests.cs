using Moq;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

/// <summary>Clasificación contable del producto (Task 5.3): grupos opcionales en el alta y "null = conservar" en la modificación.</summary>
public sealed class ProductoGruposContablesTests
{
    private sealed record Grupos(GrupoProducto Producto, GrupoIvaProducto IvaProducto, GrupoInventario Inventario);

    private static Grupos AgregarGrupos(ProductosFake fake, string sufijo = "") => new(
        fake.GruposProducto.Agregar(new GrupoProducto { Codigo = "BIENES" + sufijo, Descripcion = "Bienes" }),
        fake.GruposIvaProducto.Agregar(new GrupoIvaProducto { Codigo = "ITBIS18" + sufijo, Descripcion = "ITBIS" }),
        fake.GruposInventario.Agregar(new GrupoInventario { Codigo = "GENERAL" + sufijo, Descripcion = "General" }));

    [Fact]
    public async Task Create_ConGrupos_LosAsignaYDevuelveSusCodigos()
    {
        var fake = new ProductosFake();
        var grupos = AgregarGrupos(fake);

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object).Handle(
            new CreateProductoCommand("C1", "Producto", 1m,
                GrupoProductoId: grupos.Producto.Id, GrupoIvaProductoId: grupos.IvaProducto.Id, GrupoInventarioId: grupos.Inventario.Id),
            default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        var guardado = Assert.Single(fake.Productos.Datos);
        Assert.Equal(grupos.Producto.Id, guardado.GrupoProductoId);
        Assert.Equal(grupos.IvaProducto.Id, guardado.GrupoIvaProductoId);
        Assert.Equal(grupos.Inventario.Id, guardado.GrupoInventarioId);
        Assert.Equal("BIENES", result.Valor.GrupoProductoCodigo);
        Assert.Equal("ITBIS18", result.Valor.GrupoIvaProductoCodigo);
        Assert.Equal("GENERAL", result.Valor.GrupoInventarioCodigo);
    }

    private static Grupos AgregarSemillas(ProductosFake fake) => new(
        fake.GruposProducto.Agregar(new GrupoProducto { Codigo = "BIENES", Descripcion = "Bienes" }.ConId(GrupoContableIds.ProductoBienes)),
        fake.GruposIvaProducto.Agregar(new GrupoIvaProducto { Codigo = "ITBIS18", Descripcion = "ITBIS" }.ConId(GrupoContableIds.IvaProductoItbis18)),
        fake.GruposInventario.Agregar(new GrupoInventario { Codigo = "GENERAL", Descripcion = "General" }.ConId(GrupoContableIds.InventarioGeneral)));

    [Fact]
    public async Task Create_SinGrupos_TomaLasSemillasPorDefecto()
    {
        var fake = new ProductosFake();
        AgregarGrupos(fake, "-OTRO");
        AgregarSemillas(fake);

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object).Handle(new CreateProductoCommand("C1", "Producto", 1m), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        var guardado = Assert.Single(fake.Productos.Datos);
        Assert.Equal(GrupoContableIds.ProductoBienes, guardado.GrupoProductoId);
        Assert.Equal(GrupoContableIds.IvaProductoItbis18, guardado.GrupoIvaProductoId);
        Assert.Equal(GrupoContableIds.InventarioGeneral, guardado.GrupoInventarioId);
        Assert.Equal("BIENES", result.Valor.GrupoProductoCodigo);
        Assert.Equal("ITBIS18", result.Valor.GrupoIvaProductoCodigo);
        Assert.Equal("GENERAL", result.Valor.GrupoInventarioCodigo);
    }

    [Fact]
    public async Task Create_SinGrupos_ConUnaSemillaBorrada_LaDejaNull_YNoFalla()
    {
        var fake = new ProductosFake();
        var semillas = AgregarSemillas(fake);
        semillas.IvaProducto.IsDeleted = true;

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object).Handle(new CreateProductoCommand("C1", "Producto", 1m), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        var guardado = Assert.Single(fake.Productos.Datos);
        Assert.Equal(GrupoContableIds.ProductoBienes, guardado.GrupoProductoId);
        Assert.Null(guardado.GrupoIvaProductoId);
        Assert.Equal(GrupoContableIds.InventarioGeneral, guardado.GrupoInventarioId);
    }

    [Fact]
    public async Task Update_SinGrupos_NoAplicaSemillas_ConservaElNull()
    {
        var fake = new ProductosFake();
        AgregarSemillas(fake);
        var entity = fake.Productos.Agregar(new Producto { Codigo = "OLD", Nombre = "Old", PrecioVenta = 1, CategoriaId = fake.General.Id, UnidadMedidaBaseId = fake.Unidad.Id });

        var result = await Handler(fake).Handle(new UpdateProductoCommand(entity.Id, "OLD", "Renombrado", null, null, null, null, null, null), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Null(entity.GrupoProductoId);
        Assert.Null(entity.GrupoIvaProductoId);
        Assert.Null(entity.GrupoInventarioId);
    }

    [Theory]
    [InlineData("GrupoProductoId")]
    [InlineData("GrupoIvaProductoId")]
    [InlineData("GrupoInventarioId")]
    public async Task Create_GrupoInexistente_Devuelve400GrupoInvalidoEnSuCampo_YNoGuarda(string campo)
    {
        var fake = new ProductosFake();
        var grupos = AgregarGrupos(fake);
        var inexistente = Guid.NewGuid();

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object).Handle(
            new CreateProductoCommand("C1", "Producto", 1m,
                GrupoProductoId: campo == "GrupoProductoId" ? inexistente : grupos.Producto.Id,
                GrupoIvaProductoId: campo == "GrupoIvaProductoId" ? inexistente : grupos.IvaProducto.Id,
                GrupoInventarioId: campo == "GrupoInventarioId" ? inexistente : grupos.Inventario.Id),
            default);

        Assert.True(result.EsFallo);
        Assert.Equal("producto.grupo_invalido", result.Errores[0].Codigo);
        Assert.Equal(campo, result.Errores[0].Campo);
        Assert.Empty(fake.Productos.Datos);
    }

    [Fact]
    public async Task Update_SinGrupos_ConservaLosGuardados()
    {
        var fake = new ProductosFake();
        var grupos = AgregarGrupos(fake);
        var entity = ProductoExistente(fake, grupos);

        var result = await Handler(fake).Handle(new UpdateProductoCommand(entity.Id, "OLD", "Renombrado", null, null, null, null, null, null), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal(grupos.Producto.Id, entity.GrupoProductoId);
        Assert.Equal(grupos.IvaProducto.Id, entity.GrupoIvaProductoId);
        Assert.Equal(grupos.Inventario.Id, entity.GrupoInventarioId);
        Assert.Equal("BIENES", result.Valor.GrupoProductoCodigo);
    }

    [Fact]
    public async Task Update_CambiaUnGrupo_ConservaLosDemas()
    {
        var fake = new ProductosFake();
        var grupos = AgregarGrupos(fake);
        var servicios = fake.GruposProducto.Agregar(new GrupoProducto { Codigo = "SERVICIOS", Descripcion = "Servicios" });
        var entity = ProductoExistente(fake, grupos);

        var result = await Handler(fake).Handle(
            new UpdateProductoCommand(entity.Id, "OLD", "Old", null, null, null, null, null, null, GrupoProductoId: servicios.Id), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal(servicios.Id, entity.GrupoProductoId);
        Assert.Equal(grupos.IvaProducto.Id, entity.GrupoIvaProductoId);
        Assert.Equal("SERVICIOS", result.Valor.GrupoProductoCodigo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_GrupoNuevoInexistenteOGuidVacio_Devuelve400YNoModifica(bool guidVacio)
    {
        var fake = new ProductosFake();
        var grupos = AgregarGrupos(fake);
        var entity = ProductoExistente(fake, grupos);

        var result = await Handler(fake).Handle(
            new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, null, null, null, null, null,
                GrupoInventarioId: guidVacio ? Guid.Empty : Guid.NewGuid()),
            default);

        Assert.True(result.EsFallo);
        Assert.Equal("producto.grupo_invalido", result.Errores[0].Codigo);
        Assert.Equal("GrupoInventarioId", result.Errores[0].Campo);
        Assert.Equal(grupos.Inventario.Id, entity.GrupoInventarioId);
        Assert.Equal("Old", entity.Nombre);
        fake.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_GrupoGuardadoYaBorrado_NoSeRevalidaSiNoCambia()
    {
        var fake = new ProductosFake();
        var grupos = AgregarGrupos(fake);
        var entity = ProductoExistente(fake, grupos);
        grupos.Producto.IsDeleted = true;

        var result = await Handler(fake).Handle(new UpdateProductoCommand(entity.Id, "OLD", "Renombrado", null, null, null, null, null, null), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal(grupos.Producto.Id, entity.GrupoProductoId);
        Assert.Null(result.Valor.GrupoProductoCodigo);
    }

    private static Producto ProductoExistente(ProductosFake fake, Grupos grupos) => fake.Productos.Agregar(new Producto
    {
        Codigo = "OLD",
        Nombre = "Old",
        PrecioVenta = 1,
        CategoriaId = fake.General.Id,
        UnidadMedidaBaseId = fake.Unidad.Id,
        GrupoProductoId = grupos.Producto.Id,
        GrupoIvaProductoId = grupos.IvaProducto.Id,
        GrupoInventarioId = grupos.Inventario.Id,
    });

    private static UpdateProductoCommandHandler Handler(ProductosFake fake) =>
        new(fake.UnitOfWork.Object, new Mock<IConsultaInventario>().Object, new Mock<IRegistroMovimientosInventario>().Object);
}
