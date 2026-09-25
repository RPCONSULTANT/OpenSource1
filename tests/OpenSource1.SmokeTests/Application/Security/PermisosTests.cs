using OpenSource1.Application.Security;

namespace OpenSource1.SmokeTests.Application.Security;

public sealed class PermisosTests
{
    [Fact]
    public void Catalogo_Contiene_Los_Permisos_Esperados_Por_Modulo()
    {
        Assert.Equal("maestros.socio.consultar", Permisos.Maestros.Socio.Consultar);
        Assert.Equal("maestros.socio.agregar", Permisos.Maestros.Socio.Agregar);
        Assert.Equal("maestros.socio.modificar", Permisos.Maestros.Socio.Modificar);
        Assert.Equal("maestros.socio.eliminar", Permisos.Maestros.Socio.Eliminar);

        Assert.Equal("maestros.producto.consultar", Permisos.Maestros.Producto.Consultar);
        Assert.Equal("maestros.producto.eliminar", Permisos.Maestros.Producto.Eliminar);

        Assert.Equal("inventario.almacen.consultar", Permisos.Inventario.Almacen.Consultar);
        Assert.Equal("inventario.almacen.eliminar", Permisos.Inventario.Almacen.Eliminar);
        Assert.Equal("inventario.diario.capturar", Permisos.Inventario.Diario.Capturar);
        Assert.Equal("inventario.diario.postear", Permisos.Inventario.Diario.Postear);

        Assert.Equal("contabilidad.cuenta.consultar", Permisos.Contabilidad.Cuenta.Consultar);
        Assert.Equal("contabilidad.cuenta.eliminar", Permisos.Contabilidad.Cuenta.Eliminar);
        Assert.Equal("contabilidad.setup.consultar", Permisos.Contabilidad.Setup.Consultar);
        Assert.Equal("contabilidad.setup.eliminar", Permisos.Contabilidad.Setup.Eliminar);

        Assert.Equal("facturacion.factura.capturar", Permisos.Facturacion.Factura.Capturar);
        Assert.Equal("facturacion.factura.emitir", Permisos.Facturacion.Factura.Emitir);
        Assert.Equal("facturacion.cobro.registrar", Permisos.Facturacion.Cobro.Registrar);
        Assert.Equal("facturacion.cobro.aplicar", Permisos.Facturacion.Cobro.Aplicar);
    }

    [Fact]
    public void All_Recolecta_Exactamente_Los_26_Permisos_Del_Catalogo_Sin_Duplicados()
    {
        // 5 recursos con CRUD completo (socio, producto, almacen, cuenta, setup = 5 * 4 = 20)
        // + 2 (diario: capturar/postear) + 2 (factura: capturar/emitir) + 2 (cobro:
        // registrar/aplicar) = 26.
        Assert.Equal(26, Permisos.All.Count);
        Assert.Equal(Permisos.All.Count, Permisos.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Administrador_Tiene_Absolutamente_Todos_Los_Permisos_Del_Catalogo()
    {
        var permisos = PermisosPorRol.ParaRol(ApplicationRoles.Administrator);

        Assert.Equal(
            Permisos.All.OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
            permisos.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Supervisor_Tiene_Exactamente_Consultar_Y_La_Familia_Modificar()
    {
        var esperado = Permisos.All
            .Where(EsAccion("consultar", "modificar", "postear", "emitir", "aplicar"))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        var permisos = PermisosPorRol.ParaRol(ApplicationRoles.Supervisor)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(esperado, permisos);

        // No debe tener acciones de las familias "agregar" ni "eliminar".
        var esNoPermitido = EsAccion("agregar", "capturar", "registrar", "eliminar");
        Assert.DoesNotContain(permisos, p => esNoPermitido(p));
    }

    [Fact]
    public void Ejecutor_Tiene_Exactamente_Consultar_Y_La_Familia_Agregar()
    {
        var esperado = Permisos.All
            .Where(EsAccion("consultar", "agregar", "capturar", "registrar"))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        var permisos = PermisosPorRol.ParaRol(ApplicationRoles.Executor)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(esperado, permisos);

        // No debe tener acciones de las familias "modificar" ni "eliminar".
        var esNoPermitido = EsAccion("modificar", "postear", "emitir", "aplicar", "eliminar");
        Assert.DoesNotContain(permisos, p => esNoPermitido(p));
    }

    [Fact]
    public void Solo_Administrador_Tiene_Permisos_De_Eliminar()
    {
        var permisosEliminar = Permisos.All.Where(EsAccion("eliminar")).ToArray();
        Assert.NotEmpty(permisosEliminar);

        Assert.All(permisosEliminar, permiso =>
        {
            Assert.Contains(permiso, PermisosPorRol.ParaRol(ApplicationRoles.Administrator));
            Assert.DoesNotContain(permiso, PermisosPorRol.ParaRol(ApplicationRoles.Supervisor));
            Assert.DoesNotContain(permiso, PermisosPorRol.ParaRol(ApplicationRoles.Executor));
        });
    }

    [Fact]
    public void Rol_Desconocido_No_Recibe_Ningun_Permiso_Fino()
    {
        Assert.Empty(PermisosPorRol.ParaRol("RolQueNoExiste"));
    }

    private static Func<string, bool> EsAccion(params string[] acciones)
    {
        var set = new HashSet<string>(acciones, StringComparer.OrdinalIgnoreCase);
        return permiso => set.Contains(permiso[(permiso.LastIndexOf('.') + 1)..]);
    }
}
