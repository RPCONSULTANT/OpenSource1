namespace OpenSource1.Core.Common;

using System.Diagnostics.CodeAnalysis;

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
    private readonly T? _valor;

    private Result(bool esExito, T? valor, IReadOnlyList<Error> errores)
        : base(esExito, errores) => _valor = valor;

    /// <summary>
    /// Valor del resultado. Lanza si el resultado es fallido: leer el valor de un fallo
    /// es siempre un error de programación, y fallar ruidosamente es preferible a
    /// devolver un default indistinguible de un valor legítimo.
    /// </summary>
    public T Valor => EsExito
        ? _valor!
        : throw new InvalidOperationException(
            $"No se puede leer Valor de un resultado fallido. Errores: {string.Join("; ", Errores.Select(e => e.Codigo))}");

    /// <summary>Acceso seguro al valor, sin excepciones.</summary>
    public bool TryObtenerValor([MaybeNullWhen(false)] out T valor)
    {
        valor = _valor;
        return EsExito;
    }

    public static Result<T> Exito(T valor) => new(true, valor, []);

    public static new Result<T> Fallo(params Error[] errores)
    {
        if (errores is null || errores.Length == 0)
        {
            throw new ArgumentException("Un resultado fallido requiere al menos un error.", nameof(errores));
        }

        return new Result<T>(false, default, errores);
    }

    /// <summary>
    /// Propaga un fallo de otro resultado, conservando sus errores. Evita que cada call
    /// site reescriba a mano `Result&lt;T&gt;.Fallo(origen.Errores.ToArray())`.
    /// </summary>
    public static Result<T> Fallo(Result origen)
    {
        ArgumentNullException.ThrowIfNull(origen);

        if (origen.EsExito)
        {
            throw new ArgumentException("No se puede propagar un fallo desde un resultado exitoso.", nameof(origen));
        }

        return new Result<T>(false, default, origen.Errores);
    }
}
