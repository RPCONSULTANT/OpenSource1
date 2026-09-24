using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Features.SociosNegocio.Handlers;

internal static class SocioNegocioTestData
{
    public static CreateSocioNegocioCommand Create(
        string nombreComercial = "Comercial SRL",
        TipoSocioNegocio tipo = TipoSocioNegocio.Cliente,
        TipoDocumentoFiscal tipoDocumento = TipoDocumentoFiscal.SinDocumento,
        string? numeroDocumento = null,
        string? email = null,
        decimal limiteCredito = 0m,
        Guid? terminoPagoId = null,
        BloqueoSocioNegocio bloqueado = BloqueoSocioNegocio.Ninguno,
        string? paisCodigo = null,
        string? direccionLinea1 = null,
        string? direccionLinea2 = null) => new(
            nombreComercial, null, tipo, tipoDocumento, numeroDocumento, email, null,
            direccionLinea1, direccionLinea2, null, null, paisCodigo, terminoPagoId, limiteCredito, bloqueado);

    public static UpdateSocioNegocioCommand Update(
        Guid id,
        string nombreComercial = "Comercial SRL",
        TipoSocioNegocio tipo = TipoSocioNegocio.Cliente,
        TipoDocumentoFiscal tipoDocumento = TipoDocumentoFiscal.SinDocumento,
        string? numeroDocumento = null,
        string? email = null,
        decimal limiteCredito = 0m,
        Guid? terminoPagoId = null) => new(
            id, nombreComercial, null, tipo, tipoDocumento, numeroDocumento, email, null,
            null, null, null, null, null, terminoPagoId, limiteCredito, BloqueoSocioNegocio.Ninguno);
}
