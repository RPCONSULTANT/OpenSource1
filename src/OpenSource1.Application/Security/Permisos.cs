using System.Reflection;

namespace OpenSource1.Application.Security;

/// <summary>
/// Catálogo de permisos finos por recurso, con el formato <c>modulo.recurso.accion</c>.
/// Convive con las 4 políticas coarse de <see cref="ApplicationPolicies"/> (que se conservan
/// como alias) y se emite bajo el mismo claim JWT/cookie <c>"permission"</c> — ver
/// <c>AuthService.GetPermissions</c> y <see cref="PermisosPorRol"/>.
/// </summary>
public static class Permisos
{
    /// <summary>Prefijo usado para nombrar políticas de autorización resueltas al vuelo por
    /// <c>PermissionPolicyProvider</c> a partir de un permiso del catálogo (p.ej.
    /// <c>"permiso:maestros.socio.consultar"</c>).</summary>
    public const string PolicyPrefix = "permiso:";

    /// <summary>Construye el nombre de política correspondiente a un permiso del catálogo.</summary>
    public static string Policy(string permiso) => PolicyPrefix + permiso;

    public static class Maestros
    {
        public static class Socio
        {
            public const string Consultar = "maestros.socio.consultar";
            public const string Agregar = "maestros.socio.agregar";
            public const string Modificar = "maestros.socio.modificar";
            public const string Eliminar = "maestros.socio.eliminar";
        }

        public static class Producto
        {
            public const string Consultar = "maestros.producto.consultar";
            public const string Agregar = "maestros.producto.agregar";
            public const string Modificar = "maestros.producto.modificar";
            public const string Eliminar = "maestros.producto.eliminar";
        }
    }

    public static class Inventario
    {
        public static class Almacen
        {
            public const string Consultar = "inventario.almacen.consultar";
            public const string Agregar = "inventario.almacen.agregar";
            public const string Modificar = "inventario.almacen.modificar";
            public const string Eliminar = "inventario.almacen.eliminar";
        }

        public static class Diario
        {
            public const string Capturar = "inventario.diario.capturar";
            public const string Postear = "inventario.diario.postear";
        }
    }

    public static class Contabilidad
    {
        public static class Cuenta
        {
            public const string Consultar = "contabilidad.cuenta.consultar";
            public const string Agregar = "contabilidad.cuenta.agregar";
            public const string Modificar = "contabilidad.cuenta.modificar";
            public const string Eliminar = "contabilidad.cuenta.eliminar";
        }

        public static class Setup
        {
            public const string Consultar = "contabilidad.setup.consultar";
            public const string Agregar = "contabilidad.setup.agregar";
            public const string Modificar = "contabilidad.setup.modificar";
            public const string Eliminar = "contabilidad.setup.eliminar";
        }
    }

    public static class Facturacion
    {
        public static class Factura
        {
            public const string Capturar = "facturacion.factura.capturar";
            public const string Emitir = "facturacion.factura.emitir";
        }

        public static class Cobro
        {
            public const string Registrar = "facturacion.cobro.registrar";
            public const string Aplicar = "facturacion.cobro.aplicar";
        }
    }

    /// <summary>
    /// Todos los permisos finos del catálogo, recolectados por reflexión sobre las constantes
    /// <see langword="string"/> declaradas en <see cref="Permisos"/> y sus clases anidadas.
    /// Recolectar por reflexión (en vez de mantener una lista manual) evita que el catálogo y
    /// la lista de "todos los permisos" se desincronicen cuando se agregue un módulo nuevo.
    /// </summary>
    public static readonly IReadOnlyList<string> All = CollectAll();

    private static IReadOnlyList<string> CollectAll()
    {
        var values = new List<string>();

        // Solo se recorren las clases anidadas (Maestros, Inventario, ...): los permisos del
        // catálogo viven exclusivamente ahí. Los campos declarados directamente en Permisos
        // (PolicyPrefix, etc.) son metadatos del propio catálogo, no permisos, y deben quedar
        // fuera de "All".
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var nested in typeof(Permisos).GetNestedTypes(flags))
        {
            CollectFromType(nested, values);
        }

        return values.AsReadOnly();
    }

    private static void CollectFromType(Type type, List<string> values)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(flags))
        {
            if (field.FieldType == typeof(string) && field.IsLiteral)
            {
                values.Add((string)field.GetRawConstantValue()!);
            }
        }

        foreach (var nested in type.GetNestedTypes(flags))
        {
            CollectFromType(nested, values);
        }
    }
}
