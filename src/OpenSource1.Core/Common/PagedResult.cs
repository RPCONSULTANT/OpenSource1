namespace OpenSource1.Core.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Pagina, int TamanoPagina, long Total)
{
    public int TotalPaginas => TamanoPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanoPagina);

    public static PagedResult<T> Vacio(PageRequest peticion) =>
        new([], peticion.Pagina, peticion.TamanoPagina, 0);
}
