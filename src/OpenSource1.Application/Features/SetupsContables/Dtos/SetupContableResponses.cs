using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SetupsContables.Dtos;

/// <summary>
/// DTOs de lectura de los setups contables (Task 5.4) con el CÓDIGO de cada grupo/almacén y el número y nombre de cada cuenta,
/// para rotularlos sin otra consulta (vacíos si el grupo/cuenta ya no existe; el eje comodín va a <see langword="null"/>).
/// Propiedades <c>init</c> para que Dapper los pueble. <c>Xmin</c> es el token de concurrencia optimista del PUT.
/// </summary>
public abstract class SetupContableResponseBase
{
    public Guid Id { get; init; }
    public long Xmin { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}

public sealed class SetupGeneralResponse : SetupContableResponseBase
{
    public Guid? GrupoNegocioId { get; init; }
    public string? GrupoNegocioCodigo { get; init; }
    public Guid GrupoProductoId { get; init; }
    public string GrupoProductoCodigo { get; init; } = string.Empty;
    public Guid CuentaVentasId { get; init; }
    public string CuentaVentasNumero { get; init; } = string.Empty;
    public string CuentaVentasNombre { get; init; } = string.Empty;
    public Guid CuentaCostoVentasId { get; init; }
    public string CuentaCostoVentasNumero { get; init; } = string.Empty;
    public string CuentaCostoVentasNombre { get; init; } = string.Empty;
    public Guid CuentaDescuentoVentasId { get; init; }
    public string CuentaDescuentoVentasNumero { get; init; } = string.Empty;
    public string CuentaDescuentoVentasNombre { get; init; } = string.Empty;
    public Guid CuentaAjusteInventarioId { get; init; }
    public string CuentaAjusteInventarioNumero { get; init; } = string.Empty;
    public string CuentaAjusteInventarioNombre { get; init; } = string.Empty;
}

public sealed class SetupIvaResponse : SetupContableResponseBase
{
    public Guid? GrupoIvaNegocioId { get; init; }
    public string? GrupoIvaNegocioCodigo { get; init; }
    public Guid GrupoIvaProductoId { get; init; }
    public string GrupoIvaProductoCodigo { get; init; } = string.Empty;
    public decimal PorcentajeIva { get; init; }
    public Guid CuentaIvaVentasId { get; init; }
    public string CuentaIvaVentasNumero { get; init; } = string.Empty;
    public string CuentaIvaVentasNombre { get; init; } = string.Empty;
    public Guid? CuentaIvaComprasId { get; init; }
    public string? CuentaIvaComprasNumero { get; init; }
    public string? CuentaIvaComprasNombre { get; init; }
    public string IdentificadorIva { get; init; } = string.Empty;
    public TipoCalculoIva TipoCalculoIva { get; init; }
}

public sealed class SetupInventarioResponse : SetupContableResponseBase
{
    public Guid? AlmacenId { get; init; }
    public string? AlmacenCodigo { get; init; }
    public Guid GrupoInventarioId { get; init; }
    public string GrupoInventarioCodigo { get; init; } = string.Empty;
    public Guid CuentaInventarioId { get; init; }
    public string CuentaInventarioNumero { get; init; } = string.Empty;
    public string CuentaInventarioNombre { get; init; } = string.Empty;
    public Guid CuentaAjusteInventarioId { get; init; }
    public string CuentaAjusteInventarioNumero { get; init; } = string.Empty;
    public string CuentaAjusteInventarioNombre { get; init; } = string.Empty;
    public Guid CuentaVariacionCostoId { get; init; }
    public string CuentaVariacionCostoNumero { get; init; } = string.Empty;
    public string CuentaVariacionCostoNombre { get; init; } = string.Empty;
}
