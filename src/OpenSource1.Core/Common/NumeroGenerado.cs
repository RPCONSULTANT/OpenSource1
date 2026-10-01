namespace OpenSource1.Core.Common;

/// <summary>Número emitido (o previsto) por una serie y, si la línea alcanzó su número de aviso, la advertencia para el usuario.</summary>
public sealed record NumeroGenerado(string Numero, string? Aviso);
