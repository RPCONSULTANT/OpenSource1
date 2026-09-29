namespace OpenSource1.Core.Enums;

/// <summary>
/// Tipo de documento fiscal del socio de negocio. No se valida el formato del número (el
/// proyecto no está localizado a un país concreto). Se guarda como <c>smallint</c>.
/// </summary>
public enum TipoDocumentoFiscal : short
{
    Rnc = 1,
    Cedula = 2,
    Pasaporte = 3,
    SinDocumento = 9
}
