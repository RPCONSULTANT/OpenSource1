using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FechasRegistro;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Application.Services.Registro;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Infrastructure.Services.Registro;

/// <summary>
/// <see cref="IValidadorFechaRegistro"/> sobre <see cref="IUnitOfWork"/>: lee la configuración con la MISMA conexión y
/// transacción del posteo que la invoca. Usuario con fila propia en <see cref="ConfiguracionRegistroUsuario"/> → solo su rango;
/// si no (o proceso del sistema, <see cref="IUsuarioActual.Id"/> null) → la fila general <see cref="ConfiguracionRegistroIds.General"/>.
/// </summary>
/// <remarks>
/// <para>
/// Falla cerrado (Ruling FJ): la fila general sembrada se exige SIEMPRE, también para un usuario con excepción propia. Si
/// falta (migración sin aplicar, borrado físico o lógico, restauración) lanza <see cref="InvalidOperationException"/> igual
/// que <c>UpdateFechasRegistroGeneralCommandHandler</c>; la excepción deshace la transacción del posteo, así que no se
/// escribe nada. Se recupera volviendo a sembrar la fila.
/// </para>
/// <para>
/// El rango vigente se resuelve una sola vez por instancia (scoped: un posteo) y se reutiliza en cada llamada de ese
/// posteo. La lectura no toma bloqueo: un cambio concurrente de la configuración no afecta a un posteo ya en curso.
/// </para>
/// </remarks>
public sealed class ValidadorFechaRegistro(IUnitOfWork unitOfWork, IUsuarioActual usuario) : IValidadorFechaRegistro
{
    private (RangoFechasRegistro Rango, string Origen)? _vigente;

    public async Task<Result> ValidarAsync(DateOnly fecha, CancellationToken cancellationToken = default)
    {
        var (rango, origen) = _vigente ??= await ResolverAsync(cancellationToken);
        if (rango.Permite(fecha))
        {
            return Result.Exito();
        }

        return Result.Fallo(new Error(
            "registro.fecha_no_permitida",
            $"La fecha de registro {RangoFechasRegistro.Formatear(fecha)} no está permitida: el rango {origen} es {rango.Describir()}.",
            "FechaRegistro"));
    }

    private async Task<(RangoFechasRegistro, string)> ResolverAsync(CancellationToken cancellationToken)
    {
        var general = await unitOfWork.Repository<ConfiguracionRegistro>().FirstOrDefaultAsync(
            x => x.Id == ConfiguracionRegistroIds.General, cancellationToken: cancellationToken);
        if (general is null)
        {
            // Fila sembrada por la migración AddFechasRegistroPermitidas: sin ella la base no está al día (fallo cerrado).
            throw new InvalidOperationException("Falta la fila sembrada de la configuración general de fechas de registro.");
        }

        if (usuario.Id is { } usuarioId)
        {
            var propia = await unitOfWork.Repository<ConfiguracionRegistroUsuario>().FirstOrDefaultAsync(
                x => x.UsuarioId == usuarioId, cancellationToken: cancellationToken);
            if (propia is not null)
            {
                return (new RangoFechasRegistro(propia.PermitirRegistroDesde, propia.PermitirRegistroHasta),
                    $"permitido para el usuario {propia.NombreUsuario}");
            }
        }

        return (new RangoFechasRegistro(general.PermitirRegistroDesde, general.PermitirRegistroHasta), "general permitido");
    }
}
