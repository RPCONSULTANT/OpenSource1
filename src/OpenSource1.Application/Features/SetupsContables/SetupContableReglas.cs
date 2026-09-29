using System.Linq.Expressions;
using System.Text.RegularExpressions;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CuentasContables;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SetupsContables;

/// <summary>Reglas compartidas por los handlers de los tres setups contables (convenio en <c>SetupContableCommands.cs</c>).</summary>
internal static partial class SetupContableReglas
{
    public const string Prefijo = "setup_contable";

    /// <summary>Guid.Empty en el eje secundario se lee como comodín (null).</summary>
    public static Guid? Comodin(Guid? id) => id == Guid.Empty ? null : id;

    public static Error NoEncontrado(string nombre) => new($"{Prefijo}.no_encontrado", $"No se encontró el setup {nombre} solicitado.", "Id");

    /// <summary>
    /// Valida un eje (grupo o almacén) y devuelve su CÓDIGO para los mensajes. Nulo: <c>cualquiera</c> si el eje admite comodín,
    /// o 400 <c>setup_contable.grupo_requerido</c> si es el principal. Si se ASIGNA o CAMBIA (distinto de <paramref name="actual"/>)
    /// debe existir y no estar borrado (400 <c>setup_contable.grupo_invalido</c>, nunca <c>.no_encontrado</c>); el que no cambia
    /// no se revalida.
    /// </summary>
    public static async Task<Result<string>> ValidarEjeAsync<TEje>(
        IUnitOfWork unitOfWork, Guid? id, Guid? actual, bool principal, string campo, string nombre, Func<TEje, string> codigo,
        CancellationToken cancellationToken)
        where TEje : BaseEntity
    {
        if (id is not { } ejeId || ejeId == Guid.Empty)
        {
            return principal
                ? Result<string>.Fallo(new Error($"{Prefijo}.grupo_requerido", $"El {nombre} es obligatorio.", campo))
                : Result<string>.Exito("cualquiera");
        }

        var eje = await unitOfWork.Repository<TEje>().FirstOrDefaultAsync(x => x.Id == ejeId, cancellationToken: cancellationToken);
        if (eje is not null)
        {
            return Result<string>.Exito(codigo(eje));
        }

        return ejeId == actual
            ? Result<string>.Exito("desconocido")
            : Result<string>.Fallo(new Error($"{Prefijo}.grupo_invalido", $"El {nombre} indicado no existe.", campo));
    }

    /// <summary>Valida, en orden, cada cuenta que se asigna o cambia (ver <see cref="CuentaPosteoValidacion"/>); el primer fallo corta.</summary>
    public static async Task<Result> ValidarCuentasAsync(
        IUnitOfWork unitOfWork, IEnumerable<(Guid? Nueva, Guid? Actual, string Campo, string Etiqueta)> cuentas, CancellationToken cancellationToken)
    {
        foreach (var (nueva, actual, campo, etiqueta) in cuentas)
        {
            if (nueva is not { } cuentaId || nueva == actual)
            {
                continue;
            }

            if (cuentaId == Guid.Empty)
            {
                return Result.Fallo(new Error($"{Prefijo}.cuenta_requerida", $"La cuenta {etiqueta} es obligatoria.", campo));
            }

            var validacion = await CuentaPosteoValidacion.ValidarAsync(unitOfWork, cuentaId, Prefijo, campo, etiqueta, cancellationToken);
            if (validacion.EsFallo)
            {
                return validacion;
            }
        }

        return Result.Exito();
    }

    /// <summary>
    /// ¿Otra fila viva (no <paramref name="excluirId"/>) con la misma combinación? → 409 <c>setup_contable.conflicto</c> con los
    /// códigos. El índice único <c>NULLS NOT DISTINCT</c> es la red final ante altas concurrentes (23505 → 409 genérico).
    /// </summary>
    public static async Task<Error?> DuplicadoAsync<TSetup>(
        IUnitOfWork unitOfWork, Expression<Func<TSetup, bool>> mismaCombinacion, string descripcion, CancellationToken cancellationToken)
        where TSetup : BaseEntity
    {
        var existente = await unitOfWork.Repository<TSetup>().FirstOrDefaultAsync(mismaCombinacion, cancellationToken: cancellationToken);
        return existente is null
            ? null
            : new Error($"{Prefijo}.conflicto", $"Ya existe un setup {descripcion}.", "Id");
    }

    /// <summary>Porcentaje 0-100 con ≤5 decimales; Exento exige 0; tipo válido; identificador 1-20 (mayúsculas, dígitos, _ y -).</summary>
    public static List<Error> ValidarIva(decimal porcentaje, TipoCalculoIva tipo, string? identificador)
    {
        var errores = new List<Error>();
        if (porcentaje < 0m || porcentaje > 100m || decimal.Round(porcentaje, 5) != porcentaje)
        {
            errores.Add(new Error($"{Prefijo}.porcentaje_invalido", "El porcentaje de IVA debe estar entre 0 y 100, con 5 decimales como máximo.", "PorcentajeIva"));
        }

        if (!Enum.IsDefined(tipo))
        {
            errores.Add(new Error($"{Prefijo}.tipo_calculo_invalido", "El tipo de cálculo de IVA no es válido (Normal o Exento).", "TipoCalculoIva"));
        }
        else if (tipo == TipoCalculoIva.Exento && porcentaje != 0m)
        {
            errores.Add(new Error($"{Prefijo}.porcentaje_invalido", "Un IVA exento debe tener porcentaje 0.", "PorcentajeIva"));
        }

        if (string.IsNullOrWhiteSpace(identificador))
        {
            errores.Add(new Error($"{Prefijo}.identificador_requerido", "El identificador de IVA es obligatorio.", "IdentificadorIva"));
        }
        else if (!IdentificadorValido().IsMatch(NormalizarIdentificador(identificador)))
        {
            errores.Add(new Error(
                $"{Prefijo}.identificador_invalido",
                "El identificador de IVA debe tener entre 1 y 20 caracteres, usando solo letras, números, guion y guion bajo.",
                "IdentificadorIva"));
        }

        return errores;
    }

    public static string NormalizarIdentificador(string identificador) => identificador.Trim().ToUpperInvariant();

    [GeneratedRegex("^[A-Z0-9_-]{1,20}$")]
    private static partial Regex IdentificadorValido();
}
