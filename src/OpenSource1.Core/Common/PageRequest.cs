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

    public int Offset => (Pagina - 1) * TamanoPagina;
}
