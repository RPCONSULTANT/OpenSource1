using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Series.Dtos;

/// <summary>Serie con su línea vigente HOY resumida (último usado, próximo número y aviso) y sus protecciones.</summary>
public sealed class SerieResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public TipoDocumentoSerie TipoDocumento { get; init; }
    public string TipoNombre => TipoDocumentoSerieNombres.Nombre(TipoDocumento);
    public bool PermiteHuecos { get; init; }
    public bool Activa { get; init; }
    public string? UltimoNumeroUsado { get; init; }
    public string? ProximoNumero { get; init; }
    public string? Aviso { get; init; }
    public bool EnAviso { get; init; }
    public bool Usada { get; init; }
    public bool Asignada { get; init; }
    public long Xmin { get; init; }
}

public sealed class LineaSerieResponse
{
    public Guid Id { get; init; }
    public Guid SerieId { get; init; }
    public string NumeroInicial { get; init; } = string.Empty;
    public string NumeroFinal { get; init; } = string.Empty;
    public string? NumeroAviso { get; init; }
    public string UltimoNumeroUsado { get; init; } = string.Empty;
    public DateOnly FechaInicial { get; init; }
    public int Incremento { get; init; }
    public bool Bloqueada { get; init; }
    public bool Usada { get; init; }
    public long Xmin { get; init; }
}

public sealed record SerieDetalleResponse(SerieResponse Serie, IReadOnlyList<LineaSerieResponse> Lineas);

public sealed record ProximoNumeroResponse(string Numero, string? Aviso);
