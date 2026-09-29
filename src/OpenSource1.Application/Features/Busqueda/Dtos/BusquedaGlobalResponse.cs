namespace OpenSource1.Application.Features.Busqueda.Dtos;

/// <summary>Un resultado de la búsqueda global: <c>Ruta</c> es la ruta de la UI (Blazor) que abre el registro.</summary>
public sealed record ResultadoBusqueda(string Tipo, string Id, string Titulo, string? Subtitulo, string Ruta);

public sealed record GrupoResultadosBusqueda(string Tipo, string Titulo, IReadOnlyList<ResultadoBusqueda> Items);

/// <summary>Siempre los 6 grupos de <see cref="TiposResultadoBusqueda"/>, en ese orden, aunque estén vacíos.</summary>
public sealed record BusquedaGlobalResponse(IReadOnlyList<GrupoResultadosBusqueda> Grupos);

public static class TiposResultadoBusqueda
{
    public const string Clientes = "clientes";
    public const string Productos = "productos";
    public const string Facturas = "facturas";
    public const string BorradoresFactura = "borradoresFactura";
    public const string NotasCredito = "notasCredito";
    public const string BorradoresNotaCredito = "borradoresNotaCredito";
}
