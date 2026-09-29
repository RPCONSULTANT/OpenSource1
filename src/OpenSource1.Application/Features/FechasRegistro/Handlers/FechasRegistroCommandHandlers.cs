using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FechasRegistro.Commands;
using OpenSource1.Application.Features.FechasRegistro.Dtos;
using OpenSource1.Application.Features.Users;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.FechasRegistro.Handlers;

public sealed class UpdateFechasRegistroGeneralCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateFechasRegistroGeneralCommand, Result<FechasRegistroGeneralResponse>>
{
    public async Task<Result<FechasRegistroGeneralResponse>> Handle(
        UpdateFechasRegistroGeneralCommand request, CancellationToken cancellationToken)
    {
        if (RangoFechasRegistro.ValidarLimites(request.PermitirRegistroDesde, request.PermitirRegistroHasta) is { } error)
        {
            return Result<FechasRegistroGeneralResponse>.Fallo(error);
        }

        var repository = unitOfWork.Repository<ConfiguracionRegistro>();
        var entity = await repository.GetByIdAsync([ConfiguracionRegistroIds.General], cancellationToken);
        if (entity is null)
        {
            // Fila sembrada por la migración AddFechasRegistroPermitidas: sin ella la base no está al día.
            throw new InvalidOperationException("Falta la fila sembrada de la configuración general de fechas de registro.");
        }

        entity.PermitirRegistroDesde = request.PermitirRegistroDesde;
        entity.PermitirRegistroHasta = request.PermitirRegistroHasta;
        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<FechasRegistroGeneralResponse>.Exito(new FechasRegistroGeneralResponse
        {
            PermitirRegistroDesde = entity.PermitirRegistroDesde,
            PermitirRegistroHasta = entity.PermitirRegistroHasta,
            UpdatedAtUtc = entity.UpdatedAtUtc?.UtcDateTime,
            UpdatedBy = entity.UpdatedBy,
        });
    }
}

public sealed class CreateFechasRegistroUsuarioCommandHandler(IUnitOfWork unitOfWork, IUserAdminService usuarios)
    : IRequestHandler<CreateFechasRegistroUsuarioCommand, Result<FechasRegistroUsuarioResponse>>
{
    private const int LongitudNombreUsuario = 256;

    public async Task<Result<FechasRegistroUsuarioResponse>> Handle(
        CreateFechasRegistroUsuarioCommand request, CancellationToken cancellationToken)
    {
        var errores = new List<Error>();
        if (RangoFechasRegistro.ValidarLimites(request.PermitirRegistroDesde, request.PermitirRegistroHasta) is { } error)
        {
            errores.Add(error);
        }

        // El usuario vive en la base de Identity: se comprueba que exista y se desnormaliza su nombre de usuario (el correo, que
        // es también su UserName) para mostrarlo sin volver a consultar Identity.
        var usuario = request.UsuarioId == Guid.Empty
            ? null
            : await usuarios.GetByIdAsync(request.UsuarioId.ToString(), cancellationToken);
        if (usuario is null)
        {
            errores.Add(new Error("registro.usuario_invalido", "El usuario indicado no existe.", "UsuarioId"));
        }

        if (errores.Count > 0)
        {
            return Result<FechasRegistroUsuarioResponse>.Fallo([.. errores]);
        }

        var repository = unitOfWork.Repository<ConfiguracionRegistroUsuario>();
        if (await repository.FirstOrDefaultAsync(x => x.UsuarioId == request.UsuarioId, cancellationToken: cancellationToken) is not null)
        {
            return Result<FechasRegistroUsuarioResponse>.Fallo(new Error(
                "registro.usuario_duplicado.conflicto",
                "El usuario ya tiene fechas de registro propias: modifique su fila en lugar de agregar otra.",
                "UsuarioId"));
        }

        var nombre = string.IsNullOrWhiteSpace(usuario!.Email) ? usuario.FullName : usuario.Email;
        var entity = new ConfiguracionRegistroUsuario
        {
            UsuarioId = request.UsuarioId,
            NombreUsuario = nombre.Length <= LongitudNombreUsuario ? nombre : nombre[..LongitudNombreUsuario],
            PermitirRegistroDesde = request.PermitirRegistroDesde,
            PermitirRegistroHasta = request.PermitirRegistroHasta,
        };
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<FechasRegistroUsuarioResponse>.Exito(ToResponse(entity));
    }

    internal static FechasRegistroUsuarioResponse ToResponse(ConfiguracionRegistroUsuario x) => new()
    {
        Id = x.Id,
        UsuarioId = x.UsuarioId,
        NombreUsuario = x.NombreUsuario,
        PermitirRegistroDesde = x.PermitirRegistroDesde,
        PermitirRegistroHasta = x.PermitirRegistroHasta,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy,
    };
}

public sealed class UpdateFechasRegistroUsuarioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateFechasRegistroUsuarioCommand, Result<FechasRegistroUsuarioResponse>>
{
    public async Task<Result<FechasRegistroUsuarioResponse>> Handle(
        UpdateFechasRegistroUsuarioCommand request, CancellationToken cancellationToken)
    {
        if (RangoFechasRegistro.ValidarLimites(request.PermitirRegistroDesde, request.PermitirRegistroHasta) is { } error)
        {
            return Result<FechasRegistroUsuarioResponse>.Fallo(error);
        }

        var repository = unitOfWork.Repository<ConfiguracionRegistroUsuario>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return Result<FechasRegistroUsuarioResponse>.Fallo(FechasRegistroErrores.UsuarioNoEncontrado());
        }

        entity.PermitirRegistroDesde = request.PermitirRegistroDesde;
        entity.PermitirRegistroHasta = request.PermitirRegistroHasta;
        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<FechasRegistroUsuarioResponse>.Exito(CreateFechasRegistroUsuarioCommandHandler.ToResponse(entity));
    }
}

public sealed class DeleteFechasRegistroUsuarioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteFechasRegistroUsuarioCommand, Result>
{
    public async Task<Result> Handle(DeleteFechasRegistroUsuarioCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<ConfiguracionRegistroUsuario>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return Result.Fallo(FechasRegistroErrores.UsuarioNoEncontrado());
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Exito();
    }
}

internal static class FechasRegistroErrores
{
    public static Error UsuarioNoEncontrado() => new(
        "registro.usuario.no_encontrado", "No se encontraron las fechas de registro del usuario solicitado.", "Id");
}
