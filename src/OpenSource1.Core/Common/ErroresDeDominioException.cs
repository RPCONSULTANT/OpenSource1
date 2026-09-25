namespace OpenSource1.Core.Common;

/// <summary>
/// Válvula de escape para capas que no pueden devolver Result (constructores, factories de
/// value objects, validación dentro de una query ya en vuelo). El manejador global la
/// traduce a 400 con los errores por campo, en vez del 500 desnudo que produce hoy
/// cualquier ArgumentException del dominio.
/// </summary>
public sealed class ErroresDeDominioException : Exception
{
    public ErroresDeDominioException(IReadOnlyList<Error> errores)
        : base(string.Join("; ", errores.Select(e => e.Mensaje)))
    {
        if (errores.Count == 0)
        {
            throw new ArgumentException("Se requiere al menos un error.", nameof(errores));
        }

        Errores = errores;
    }

    public ErroresDeDominioException(Error error) : this([error]) { }

    public IReadOnlyList<Error> Errores { get; }
}
