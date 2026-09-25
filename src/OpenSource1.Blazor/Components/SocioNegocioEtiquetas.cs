using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>Rótulos en español de los enums del socio de negocio, compartidos por la UI y las exportaciones.</summary>
public static class SocioNegocioEtiquetas
{
    public static readonly IReadOnlyList<TipoSocioNegocio> Tipos =
        [TipoSocioNegocio.Cliente, TipoSocioNegocio.Proveedor, TipoSocioNegocio.Ambos];

    public static readonly IReadOnlyList<TipoDocumentoFiscal> TiposDocumento =
        [TipoDocumentoFiscal.SinDocumento, TipoDocumentoFiscal.Rnc, TipoDocumentoFiscal.Cedula, TipoDocumentoFiscal.Pasaporte];

    public static readonly IReadOnlyList<BloqueoSocioNegocio> Bloqueos =
        [BloqueoSocioNegocio.Ninguno, BloqueoSocioNegocio.Facturacion, BloqueoSocioNegocio.Todo];

    public static string Tipo(TipoSocioNegocio valor) => valor switch
    {
        TipoSocioNegocio.Cliente => "Cliente",
        TipoSocioNegocio.Proveedor => "Proveedor",
        TipoSocioNegocio.Ambos => "Cliente y proveedor",
        _ => $"Desconocido ({(short)valor})"
    };

    public static string TipoDocumento(TipoDocumentoFiscal valor) => valor switch
    {
        TipoDocumentoFiscal.Rnc => "RNC",
        TipoDocumentoFiscal.Cedula => "Cédula",
        TipoDocumentoFiscal.Pasaporte => "Pasaporte",
        TipoDocumentoFiscal.SinDocumento => "Sin documento",
        _ => $"Desconocido ({(short)valor})"
    };

    public static string Bloqueo(BloqueoSocioNegocio valor) => valor switch
    {
        BloqueoSocioNegocio.Ninguno => "Sin bloqueo",
        BloqueoSocioNegocio.Facturacion => "Bloqueado para facturar",
        BloqueoSocioNegocio.Todo => "Bloqueado totalmente",
        _ => $"Desconocido ({(short)valor})"
    };

    /// <summary>"RNC 101234567" o "—" cuando no tiene documento.</summary>
    public static string Documento(TipoDocumentoFiscal tipo, string? numero) =>
        string.IsNullOrWhiteSpace(numero) ? "—" : $"{TipoDocumento(tipo)} {numero}";
}
