using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Series;

public sealed record SerieSearchCriteria(string? Codigo = null, TipoDocumentoSerie? Tipo = null, bool? Activa = null);
