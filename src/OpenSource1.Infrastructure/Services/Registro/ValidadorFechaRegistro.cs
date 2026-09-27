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
/// El rango vigente se resuelve una vez por instancia (scoped).
/// </summary>
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

        var general = await unitOfWork.Repository<ConfiguracionRegistro>().FirstOrDefaultAsync(
            x => x.Id == ConfiguracionRegistroIds.General, cancellationToken: cancellationToken);
        return (new RangoFechasRegistro(general?.PermitirRegistroDesde, general?.PermitirRegistroHasta), "general permitido");
    }
}
