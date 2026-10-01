using MediatR;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Series.Commands;

public sealed record CreateSerieCommand(string Codigo, string Descripcion, TipoDocumentoSerie TipoDocumento, bool PermiteHuecos, bool Activa)
    : IRequest<Result<SerieResponse>>;

public sealed record UpdateSerieCommand(
    Guid Id, string Codigo, string Descripcion, TipoDocumentoSerie TipoDocumento, bool PermiteHuecos, bool Activa, long Xmin)
    : IRequest<Result<SerieResponse>>;

/// <summary>Borrado lógico con el <c>xmin</c> que vio el usuario (obsoleto -&gt; 409).</summary>
public sealed record DeleteSerieCommand(Guid Id, long Xmin) : IRequest<Result>;

public sealed record CreateLineaSerieCommand(
    Guid SerieId, string NumeroInicial, string NumeroFinal, string? NumeroAviso, string? UltimoNumeroUsado, DateOnly FechaInicial,
    int Incremento, bool Bloqueada) : IRequest<Result<LineaSerieResponse>>;

public sealed record UpdateLineaSerieCommand(
    Guid SerieId, Guid Id, string NumeroInicial, string NumeroFinal, string? NumeroAviso, DateOnly FechaInicial, int Incremento,
    bool Bloqueada, long Xmin) : IRequest<Result<LineaSerieResponse>>;

public sealed record DeleteLineaSerieCommand(Guid SerieId, Guid Id) : IRequest<Result>;
