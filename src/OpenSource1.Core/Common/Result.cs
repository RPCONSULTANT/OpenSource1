namespace OpenSource1.Core.Common;

public class Result
{
    protected Result(bool esExito, IReadOnlyList<Error> errores)
    {
        EsExito = esExito;
        Errores = errores;
    }

    public bool EsExito { get; }
    public bool EsFallo => !EsExito;
    public IReadOnlyList<Error> Errores { get; }

    public static Result Exito() => new(true, []);

    public static Result Fallo(params Error[] errores)
    {
        if (errores is null || errores.Length == 0)
        {
            throw new ArgumentException("Un resultado fallido requiere al menos un error.", nameof(errores));
        }

        return new Result(false, errores);
    }
}

public sealed class Result<T> : Result
{
    private Result(bool esExito, T? valor, IReadOnlyList<Error> errores)
        : base(esExito, errores) => Valor = valor;

    public T? Valor { get; }

    public static Result<T> Exito(T valor) => new(true, valor, []);

    public static new Result<T> Fallo(params Error[] errores)
    {
        if (errores is null || errores.Length == 0)
        {
            throw new ArgumentException("Un resultado fallido requiere al menos un error.", nameof(errores));
        }

        return new Result<T>(false, default, errores);
    }
}
