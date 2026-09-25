namespace OpenSource1.Core.Common;

/// <summary>Error de negocio con código estable para que la API y la UI lo traduzcan.</summary>
public readonly record struct Error(string Codigo, string Mensaje, string? Campo = null);
