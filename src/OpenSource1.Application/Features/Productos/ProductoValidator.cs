using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Storage;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Productos;

/// <summary>
/// Validación compartida entre Create/Update (mismo patrón que <c>SocioNegocioValidator</c>). Solo reglas que no requieren
/// base de datos: la existencia de la categoría y de la unidad base se comprueba en los handlers. Cada error lleva el nombre
/// del campo del DTO.
/// </summary>
internal static class ProductoValidator
{
    // numeric(18,4): 14 dígitos enteros.
    private const decimal ImporteMaximo = 99_999_999_999_999.9999m;

    public static IReadOnlyList<Error> Validar(IDatosProducto datos)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(datos.Codigo))
        {
            errores.Add(new Error("producto.codigo_requerido", "El código es obligatorio.", nameof(datos.Codigo)));
        }
        else if (datos.Codigo.Trim().Length > 50)
        {
            errores.Add(new Error("producto.codigo_invalido", "El código no puede superar los 50 caracteres.", nameof(datos.Codigo)));
        }

        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            errores.Add(new Error("producto.nombre_requerido", "El nombre es obligatorio.", nameof(datos.Nombre)));
        }
        else if (datos.Nombre.Trim().Length > 200)
        {
            errores.Add(new Error("producto.nombre_invalido", "El nombre no puede superar los 200 caracteres.", nameof(datos.Nombre)));
        }

        ValidarImporte(errores, datos.PrecioVenta, nameof(datos.PrecioVenta), "precio de venta", "producto.precio_venta_invalido");
        ValidarImporte(errores, datos.CostoEstandar, nameof(datos.CostoEstandar), "costo estándar", "producto.costo_estandar_invalido");

        if (!Enum.IsDefined(datos.MetodoCosteo))
        {
            errores.Add(new Error("producto.metodo_costeo_invalido", "El método de costeo no es válido (1=Promedio).", nameof(datos.MetodoCosteo)));
        }

        if (!Enum.IsDefined(datos.Bloqueado))
        {
            errores.Add(new Error("producto.bloqueo_invalido", "El nivel de bloqueo no es válido (0=Ninguno, 1=Venta, 2=Todo).", nameof(datos.Bloqueado)));
        }

        // La ruta acaba en un borrado de fichero al sustituir la imagen: solo se acepta /uploads/productos/<nombre>.
        if (RutaImagen.Validar(datos.ImagePath, RutaImagen.CarpetaProductos) is { } errorImagen)
        {
            errores.Add(errorImagen);
        }

        return errores;
    }

    private static void ValidarImporte(List<Error> errores, decimal valor, string campo, string etiqueta, string codigo)
    {
        if (valor < 0 || valor > ImporteMaximo)
        {
            errores.Add(new Error(codigo, $"El {etiqueta} debe ser un importe mayor o igual a cero.", campo));
        }
        else if (decimal.Round(valor, 4) != valor)
        {
            // numeric(18,4): con más decimales Postgres redondearía en silencio.
            errores.Add(new Error(codigo, $"El {etiqueta} admite como máximo 4 decimales.", campo));
        }
    }
}
