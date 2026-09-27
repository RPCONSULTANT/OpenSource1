namespace OpenSource1.Core.Common;

public sealed record PageRequest(
    int Pagina = 1,
    int TamanoPagina = PageRequest.TamanoPorDefecto,
    string? OrdenarPor = null,
    bool Descendente = true)
{
    public const int TamanoPorDefecto = 50;
    public const int TamanoMaximo = 200;

    public PageRequest Normalizar() => this with
    {
        Pagina = Pagina < 1 ? 1 : Pagina,
        TamanoPagina = TamanoPagina switch
        {
            < 1 => TamanoPorDefecto,
            > TamanoMaximo => TamanoMaximo,
            _ => TamanoPagina,
        },
    };

    /// <summary>
    /// Filas a saltar, <c>(Pagina - 1) * TamanoPagina</c> calculado en 64 bits y ACOTADO a [0, <see cref="int.MaxValue"/>]
    /// (Task 7.5): una página enorme desbordaba el <c>int</c> y daba un OFFSET negativo (500 en Postgres). Acotado, esa página
    /// queda más allá de cualquier dato real y el listado responde 200 con una página vacía que conserva la página pedida.
    /// Las vistas de la Fase 7 validan antes con <c>PaginacionValidacion</c> y responden 400 (Campo <c>Pagina</c>).
    /// </summary>
    public int Offset => (int)Math.Clamp((long)(Pagina - 1) * TamanoPagina, 0L, int.MaxValue);
}
