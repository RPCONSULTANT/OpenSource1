using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado de <c>api/series</c> (spec no-series, Parte 3): series de numeración, sus líneas y la vista previa del próximo
/// número. Consultar = CanConsult; escrituras = CanAdministrar en la API. Los 400/409 traen los mensajes reales de la API
/// (<c>Errors</c> en un 400; <c>Message</c> en un 409) que se muestran tal cual.
/// </summary>
public interface ISerieApiClient
{
    /// <summary>Listado paginado (por defecto 1ª página de 50, por código ascendente). Un error de la API lanza <see cref="HttpRequestException"/>.</summary>
    Task<PagedResult<SerieResponse>> ListAsync(SerieFiltro? filtro = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>Series activas de un tipo, por código (hasta <see cref="PageRequest.TamanoMaximo"/>), para los selectores de los documentos.</summary>
    Task<IReadOnlyList<SerieResponse>> ActivasDelTipoAsync(TipoDocumentoSerie tipo, CancellationToken cancellationToken = default);

    /// <summary>Serie con todas sus líneas. <see langword="null"/> = no existe (404).</summary>
    Task<SerieDetalleResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Próximo número de la serie hoy, SIN reservarlo (vista previa), con el aviso si la línea lo alcanzó. Un 400 explica por qué no se puede numerar.</summary>
    Task<VentaOperationResult<ProximoNumeroResponse>> ProximoAsync(Guid id, CancellationToken cancellationToken = default);

    Task<VentaOperationResult<SerieResponse>> CreateAsync(SerieInput input, CancellationToken cancellationToken = default);

    Task<VentaOperationResult<SerieResponse>> UpdateAsync(Guid id, SerieInput input, long xmin, CancellationToken cancellationToken = default);

    /// <summary>Borrado con el <c>xmin</c> que vio el usuario (la API responde 409 si quedó obsoleto).</summary>
    Task<VentaOperationResult<bool>> DeleteAsync(Guid id, long xmin, CancellationToken cancellationToken = default);

    /// <summary>Alta de una línea. <c>UltimoNumeroUsado</c> solo se admite en el alta (null = línea sin usar).</summary>
    Task<VentaOperationResult<LineaSerieResponse>> CreateLineaAsync(Guid serieId, LineaSerieInput input, CancellationToken cancellationToken = default);

    /// <summary>Modificación (incluye bloquear/desbloquear con <c>Bloqueada</c>). <c>UltimoNumeroUsado</c> no viaja: el contador no se edita.</summary>
    Task<VentaOperationResult<LineaSerieResponse>> UpdateLineaAsync(
        Guid serieId, Guid lineaId, LineaSerieInput input, long xmin, CancellationToken cancellationToken = default);

    Task<VentaOperationResult<bool>> DeleteLineaAsync(Guid serieId, Guid lineaId, CancellationToken cancellationToken = default);
}

/// <summary>Filtros del listado de series (<c>Tipo</c> como entero 1–8, igual que la API; null = sin filtro).</summary>
public sealed record SerieFiltro(string? Codigo, int? Tipo, bool? Activa);

/// <summary>Cuerpo de alta/modificación de una serie (el <c>Xmin</c> de la modificación va aparte).</summary>
public sealed record SerieInput(string Codigo, string Descripcion, TipoDocumentoSerie TipoDocumento, bool PermiteHuecos, bool Activa);

/// <summary>Cuerpo de alta/modificación de una línea de serie (el <c>Xmin</c> de la modificación va aparte).</summary>
public sealed record LineaSerieInput(
    string NumeroInicial, string NumeroFinal, string? NumeroAviso, string? UltimoNumeroUsado, DateOnly FechaInicial, int Incremento, bool Bloqueada);
