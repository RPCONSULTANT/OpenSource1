using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Almacenes.Handlers;

public sealed class CreateAlmacenCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateAlmacenCommand, Result<AlmacenResponse>>
{
    public async Task<Result<AlmacenResponse>> Handle(CreateAlmacenCommand request, CancellationToken cancellationToken)
    {
        var errores = AlmacenValidator.Validar(
            request.Codigo, request.Nombre, request.DireccionLinea1, request.DireccionLinea2, request.Ciudad, request.PaisCodigo);

        if (errores.Count > 0)
        {
            return Result<AlmacenResponse>.Fallo([.. errores]);
        }

        var entity = new Almacen
        {
            Codigo = AlmacenValidator.NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            DireccionLinea1 = Normalizar(request.DireccionLinea1),
            DireccionLinea2 = Normalizar(request.DireccionLinea2),
            Ciudad = Normalizar(request.Ciudad),
            PaisCodigo = NormalizarPais(request.PaisCodigo),
            Bloqueado = request.Bloqueado,
            EsPredeterminado = request.EsPredeterminado
        };

        var repository = unitOfWork.Repository<Almacen>();

        if (request.EsPredeterminado)
        {
            // Marcar este almacén como predeterminado desmarca el anterior en la misma transacción:
            // BeginTransactionAsync -> quitar la marca del actual -> SaveChangesAsync -> marcar el
            // nuevo -> CommitAsync. Dos SaveChanges porque el índice único parcial no es diferible.
            await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

            var actual = await repository.FirstOrDefaultAsync(
                x => x.EsPredeterminado, asTracking: true, cancellationToken: cancellationToken);
            if (actual is not null)
            {
                actual.EsPredeterminado = false;
                repository.Update(actual);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await repository.AddAsync(entity, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        else
        {
            await repository.AddAsync(entity, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<AlmacenResponse>.Exito(ToResponse(entity));
    }

    public static AlmacenResponse ToResponse(Almacen x) => new()
    {
        Id = x.Id,
        Codigo = x.Codigo,
        Nombre = x.Nombre,
        DireccionLinea1 = x.DireccionLinea1,
        DireccionLinea2 = x.DireccionLinea2,
        Ciudad = x.Ciudad,
        PaisCodigo = x.PaisCodigo,
        Bloqueado = x.Bloqueado,
        EsPredeterminado = x.EsPredeterminado,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };

    internal static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    internal static string? NormalizarPais(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim().ToUpperInvariant();
}
