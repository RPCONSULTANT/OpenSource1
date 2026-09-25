using OpenSource1.Core.Common;

namespace OpenSource1.Application.Storage;

/// <summary>
/// Regla ÚNICA de las rutas de imagen que se guardan en base de datos (<c>ImagePath</c> de socios,
/// productos y perfil de usuario). La UI las genera como <c>/uploads/&lt;carpeta&gt;/&lt;nombre&gt;</c>; la API
/// no debe aceptar nada más, porque esa ruta acaba en una operación de borrado de ficheros cuando
/// se sustituye la imagen. Se aplica en la API (rechazo con 400) y también en el servicio de
/// almacenamiento (defensa en profundidad).
/// </summary>
/// <remarks>
/// Válido: <c>null</c>, cadena vacía, o <c>/uploads/&lt;carpeta&gt;/&lt;nombre&gt;</c> con la carpeta de la
/// entidad (comparación exacta, sensible a mayúsculas) y un <c>&lt;nombre&gt;</c> de 1-255 caracteres
/// <c>[A-Za-z0-9._-]</c> que no empieza por punto, no contiene <c>..</c> y no es <c>.</c>/<c>..</c>. Al
/// admitir solo ese alfabeto quedan excluidos separadores (<c>/</c>, <c>\</c>), caracteres de control,
/// <c>%</c> (secuencias codificadas como <c>%2e</c>/<c>%2f</c>), <c>:</c> (flujos alternos de Windows) y
/// espacios. Los nombres que genera la propia UI (<c>cliente-&lt;guid&gt;.png</c>) lo cumplen.
/// </remarks>
public static class RutaImagen
{
    public const string Campo = "ImagePath";
    public const string Prefijo = "/uploads/";

    public const string CarpetaClientes = "clientes";
    public const string CarpetaProductos = "productos";
    public const string CarpetaUsuarios = "users";

    public const string CodigoError = "imagen.ruta_invalida";
    public const string MensajeError = "La ruta de la imagen no es válida.";

    private const int LongitudMaximaNombre = 255;

    public static bool EsValida(string? ruta, string carpeta) => MensajeDeError(ruta, carpeta) is null;

    /// <summary>Error (con <c>Campo = ImagePath</c>) si la ruta no es aceptable; <c>null</c> si lo es.</summary>
    public static Error? Validar(string? ruta, string carpeta) =>
        MensajeDeError(ruta, carpeta) is { } mensaje ? new Error(CodigoError, mensaje, Campo) : null;

    /// <summary>Nombre de fichero de una ruta ya válida (lo que sigue a <c>/uploads/&lt;carpeta&gt;/</c>).</summary>
    public static string NombreDe(string rutaValida, string carpeta) => rutaValida[(Prefijo.Length + carpeta.Length + 1)..];

    private static string? MensajeDeError(string? ruta, string carpeta)
    {
        if (string.IsNullOrEmpty(ruta))
        {
            return null;
        }

        var prefijo = $"{Prefijo}{carpeta}/";
        if (!ruta.StartsWith(prefijo, StringComparison.Ordinal))
        {
            return $"La ruta de la imagen debe ser {Prefijo}{carpeta}/<nombre>.";
        }

        var nombre = ruta[prefijo.Length..];
        if (nombre.Length is 0 or > LongitudMaximaNombre
            || nombre[0] == '.'
            || nombre.Contains("..", StringComparison.Ordinal)
            || !nombre.All(EsCaracterPermitido))
        {
            return "El nombre del fichero de la imagen no es válido (solo letras, números, punto, guion y guion bajo).";
        }

        return null;
    }

    private static bool EsCaracterPermitido(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_';
}
