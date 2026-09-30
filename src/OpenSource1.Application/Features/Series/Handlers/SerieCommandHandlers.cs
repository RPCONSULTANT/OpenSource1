using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Series.Commands;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Series.Handlers;

public sealed class CreateSerieCommandHandler(IUnitOfWork unitOfWork, ISerieReadRepository lectura)
    : IRequestHandler<CreateSerieCommand, Result<SerieResponse>>
{
    public async Task<Result<SerieResponse>> Handle(CreateSerieCommand request, CancellationToken cancellationToken)
    {
        var codigo = SerieReglas.NormalizarCodigo(request.Codigo);
        var errores = SerieReglas.ValidarCabecera(codigo, request.Descripcion, request.TipoDocumento);
        if (errores.Count > 0)
        {
            return Result<SerieResponse>.Fallo([.. errores]);
        }

        var repositorio = unitOfWork.Repository<Serie>();
        if (await repositorio.FirstOrDefaultAsync(x => x.Codigo == codigo, cancellationToken: cancellationToken) is not null)
        {
            return Result<SerieResponse>.Fallo(CodigoDuplicado(codigo));
        }

        var entity = new Serie
        {
            Codigo = codigo,
            Descripcion = request.Descripcion.Trim(),
            TipoDocumento = request.TipoDocumento,
            PermiteHuecos = request.PermiteHuecos,
            Activa = request.Activa,
        };
        await repositorio.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SerieResponse>.Exito((await lectura.GetByIdAsync(entity.Id, Hoy(), cancellationToken))!.Serie);
    }

    internal static Error CodigoDuplicado(string codigo) =>
        new("serie.codigo_duplicado.conflicto", $"Ya existe una serie con el código {codigo}.", "Codigo");

    internal static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.UtcNow);
}

/// <summary>
/// Modificación con <c>xmin</c>. El tipo no cambia si la serie está usada, asignada o referenciada por plantillas/lotes (409), ni si
/// sus líneas se solaparían con las del tipo nuevo (400). Una serie asignada en la configuración no se desactiva (NS5). La fila de la
/// serie se bloquea antes de comprobar su uso: un posteo en curso termina antes (mismo orden que el motor).
/// </summary>
public sealed class UpdateSerieCommandHandler(IUnitOfWork unitOfWork, ISerieReadRepository lectura)
    : IRequestHandler<UpdateSerieCommand, Result<SerieResponse>>
{
    public async Task<Result<SerieResponse>> Handle(UpdateSerieCommand request, CancellationToken cancellationToken)
    {
        var codigo = SerieReglas.NormalizarCodigo(request.Codigo);
        var errores = SerieReglas.ValidarCabecera(codigo, request.Descripcion, request.TipoDocumento);
        if (errores.Count > 0)
        {
            return Result<SerieResponse>.Fallo([.. errores]);
        }

        // Sin CommitAsync, salir del "await using" deshace la transacción.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (await lectura.BloquearSerieAsync(request.Id, cancellationToken) is null)
        {
            return Result<SerieResponse>.Fallo(SerieReglas.NoEncontrada());
        }

        var repositorio = unitOfWork.Repository<Serie>();
        var entity = (await repositorio.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken))!;
        if (await repositorio.FirstOrDefaultAsync(x => x.Codigo == codigo && x.Id != request.Id, cancellationToken: cancellationToken) is not null)
        {
            return Result<SerieResponse>.Fallo(CreateSerieCommandHandler.CodigoDuplicado(codigo));
        }

        var uso = await lectura.UsoAsync(request.Id, cancellationToken);
        if (request.TipoDocumento != entity.TipoDocumento)
        {
            if (uso.Usada || uso.Asignada || uso.Referenciada)
            {
                return Result<SerieResponse>.Fallo(new Error(
                    "serie.tipo_en_uso.conflicto",
                    "No se puede cambiar el tipo de una serie que ya emitió números, está asignada en la configuración o la usan plantillas o lotes de diario.",
                    "TipoDocumento"));
            }

            await lectura.BloquearTipoAsync(request.TipoDocumento, cancellationToken);
            var propias = await unitOfWork.Repository<LineaSerie>().ListAsync(x => x.SerieId == request.Id, cancellationToken);
            var delTipo = await lectura.LineasDelTipoAsync(request.TipoDocumento, cancellationToken);
            foreach (var linea in propias)
            {
                if (SerieReglas.Solapada(linea.NumeroInicial, linea.NumeroFinal, delTipo, linea.Id) is { } solapada)
                {
                    return Result<SerieResponse>.Fallo(solapada with { Campo = "TipoDocumento" });
                }
            }
        }

        if (!request.Activa && entity.Activa && uso.Asignada)
        {
            return Result<SerieResponse>.Fallo(new Error(
                "serie.asignada.conflicto",
                "La serie está asignada en la configuración de numeración: asigne otra serie a ese tipo antes de desactivarla.",
                "Activa"));
        }

        repositorio.EstablecerVersionOriginal(entity, request.Xmin);
        entity.Codigo = codigo;
        entity.Descripcion = request.Descripcion.Trim();
        entity.TipoDocumento = request.TipoDocumento;
        entity.PermiteHuecos = request.PermiteHuecos;
        entity.Activa = request.Activa;
        repositorio.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<SerieResponse>.Exito((await lectura.GetByIdAsync(entity.Id, CreateSerieCommandHandler.Hoy(), cancellationToken))!.Serie);
    }
}

/// <summary>Borrado lógico de una serie sin uso (y de sus líneas). Usada, asignada o referenciada -&gt; 409 con el motivo.</summary>
public sealed class DeleteSerieCommandHandler(IUnitOfWork unitOfWork, ISerieReadRepository lectura) : IRequestHandler<DeleteSerieCommand, Result>
{
    public async Task<Result> Handle(DeleteSerieCommand request, CancellationToken cancellationToken)
    {
        // Series (FOR UPDATE) antes que sus líneas: mismo orden que el motor.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (await lectura.BloquearSerieAsync(request.Id, cancellationToken) is null)
        {
            return Result.Fallo(SerieReglas.NoEncontrada());
        }

        var uso = await lectura.UsoAsync(request.Id, cancellationToken);
        if (uso.Usada)
        {
            return Result.Fallo(new Error("serie.usada.conflicto", "La serie ya emitió números: no se puede eliminar; desactívela.", "Id"));
        }

        if (uso.Asignada)
        {
            return Result.Fallo(new Error(
                "serie.asignada.conflicto", "La serie está asignada en la configuración de numeración: no se puede eliminar.", "Id"));
        }

        if (uso.Referenciada)
        {
            return Result.Fallo(new Error(
                "serie.en_uso.conflicto", "La serie la usan plantillas o lotes de diario de inventario: no se puede eliminar.", "Id"));
        }

        var repositorio = unitOfWork.Repository<Serie>();
        var entity = (await repositorio.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken))!;
        var lineas = unitOfWork.Repository<LineaSerie>();
        foreach (var linea in await lineas.ListAsync(x => x.SerieId == request.Id, cancellationToken))
        {
            var rastreada = await lineas.FirstOrDefaultAsync(x => x.Id == linea.Id, asTracking: true, cancellationToken: cancellationToken);
            lineas.Remove(rastreada!);
        }

        repositorio.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);
        return Result.Exito();
    }
}
