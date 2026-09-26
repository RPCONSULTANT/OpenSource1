using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.GruposContables;

/// <summary>
/// Regla común para asignar un grupo contable a un maestro (productos y socios, Task 5.3), usada por
/// <c>ProductoReglas</c> y <c>SocioNegocioReglas</c>:
/// <list type="bullet">
/// <item>Alta sin grupo (<paramref name="solicitado"/> null con <c>semillaPorDefecto</c>): se asigna la semilla por defecto si existe
/// (no borrada); si la semilla fue borrada lógicamente, queda null y el alta NO falla.</item>
/// <item>Grupo que se ASIGNA o CAMBIA (distinto de <c>actual</c>): debe existir y no estar borrado; si no, error del cuerpo
/// (400, nunca <c>.no_encontrado</c>).</item>
/// <item>Grupo que no cambia: no se revalida (un maestro con un grupo ya colgante sigue siendo editable).</item>
/// </list>
/// </summary>
internal static class ResolucionGrupoContable
{
    /// <summary><see cref="Id"/> es el valor a guardar en el maestro; <see cref="Grupo"/>, la fila (null si no hay o ya no existe).</summary>
    public readonly record struct Resultado<TGrupo>(Guid? Id, TGrupo? Grupo, Error? Error) where TGrupo : GrupoContable;

    public static async Task<Resultado<TGrupo>> ResolverAsync<TGrupo>(
        IUnitOfWork unitOfWork, Guid? solicitado, Guid? actual, Guid? semillaPorDefecto, Error errorSiNoExiste, CancellationToken cancellationToken)
        where TGrupo : GrupoContable
    {
        if (solicitado is null && semillaPorDefecto is { } semilla)
        {
            var porDefecto = await BuscarAsync<TGrupo>(unitOfWork, semilla, cancellationToken);
            return new Resultado<TGrupo>(porDefecto?.Id, porDefecto, null);
        }

        var grupo = await BuscarAsync<TGrupo>(unitOfWork, solicitado, cancellationToken);
        return grupo is null && solicitado is not null && solicitado != actual
            ? new Resultado<TGrupo>(null, null, errorSiNoExiste)
            : new Resultado<TGrupo>(solicitado, grupo, null);
    }

    // FirstOrDefaultAsync (consulta) y no Find: solo la consulta aplica el filtro global de soft delete.
    private static Task<TGrupo?> BuscarAsync<TGrupo>(IUnitOfWork unitOfWork, Guid? id, CancellationToken cancellationToken)
        where TGrupo : GrupoContable =>
        id is { } grupoId
            ? unitOfWork.Repository<TGrupo>().FirstOrDefaultAsync(x => x.Id == grupoId, cancellationToken: cancellationToken)
            : Task.FromResult<TGrupo?>(null);
}
