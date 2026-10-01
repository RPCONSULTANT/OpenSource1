using MediatR;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Series.Commands;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Series.Handlers;

/// <summary>
/// Alta de una línea. La serie se bloquea (<c>FOR UPDATE</c>) y después el tipo (bloqueo consultivo) antes de comprobar la fecha
/// duplicada y el solapamiento con las líneas de las series del mismo tipo (Review Focus 1).
/// </summary>
public sealed class CreateLineaSerieCommandHandler(IUnitOfWork unitOfWork, ISerieReadRepository lectura)
    : IRequestHandler<CreateLineaSerieCommand, Result<LineaSerieResponse>>
{
    public async Task<Result<LineaSerieResponse>> Handle(CreateLineaSerieCommand request, CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (await lectura.BloquearSerieAsync(request.SerieId, cancellationToken) is not { } tipo)
        {
            return Result<LineaSerieResponse>.Fallo(SerieReglas.NoEncontrada());
        }

        var aviso = Normalizar(request.NumeroAviso);
        var ultimo = Normalizar(request.UltimoNumeroUsado);
        var errores = SerieReglas.ValidarLinea(request.NumeroInicial, request.NumeroFinal, aviso, ultimo, request.Incremento, request.FechaInicial);
        if (errores.Count > 0)
        {
            return Result<LineaSerieResponse>.Fallo([.. errores]);
        }

        var repositorio = unitOfWork.Repository<LineaSerie>();
        if (await LineaSerieReglas.FechaDuplicadaAsync(repositorio, request.SerieId, request.FechaInicial, null, cancellationToken) is { } duplicada)
        {
            return Result<LineaSerieResponse>.Fallo(duplicada);
        }

        await lectura.BloquearTipoAsync(tipo, cancellationToken);
        var delTipo = await lectura.LineasDelTipoAsync(tipo, cancellationToken);
        if (SerieReglas.Solapada(request.NumeroInicial, request.NumeroFinal, delTipo, null) is { } solapada)
        {
            return Result<LineaSerieResponse>.Fallo(solapada);
        }

        var entity = new LineaSerie
        {
            SerieId = request.SerieId,
            NumeroInicial = request.NumeroInicial,
            NumeroFinal = request.NumeroFinal,
            NumeroAviso = aviso,
            UltimoNumeroUsado = ultimo ?? string.Empty,
            FechaInicial = request.FechaInicial,
            Incremento = request.Incremento,
            Bloqueada = request.Bloqueada,
        };
        await repositorio.AddAsync(entity, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<LineaSerieResponse>.Exito(await LineaSerieReglas.LeerAsync(lectura, entity, cancellationToken));
    }

    internal static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// Modificación con <c>xmin</c>. Una línea USADA conserva su número inicial (y con él prefijo y ancho: el final y el aviso deben
/// compartirlos) y su final solo crece (NS-S1); lo demás (aviso, fecha, incremento, bloqueo) se puede cambiar. Una línea sin usar se
/// puede rehacer entera (si cambia el número inicial, el contador se vacía). La serie se bloquea antes de leer la línea: un posteo en
/// curso termina antes de decidir si la línea está usada.
/// </summary>
public sealed class UpdateLineaSerieCommandHandler(IUnitOfWork unitOfWork, ISerieReadRepository lectura)
    : IRequestHandler<UpdateLineaSerieCommand, Result<LineaSerieResponse>>
{
    public async Task<Result<LineaSerieResponse>> Handle(UpdateLineaSerieCommand request, CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var tipo = await lectura.BloquearSerieAsync(request.SerieId, cancellationToken);
        var repositorio = unitOfWork.Repository<LineaSerie>();
        var entity = tipo is null
            ? null
            : await repositorio.FirstOrDefaultAsync(x => x.Id == request.Id && x.SerieId == request.SerieId, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<LineaSerieResponse>.Fallo(SerieReglas.LineaNoEncontrada());
        }

        var usada = CalculoNumeroSerie.EstaUsada(entity.NumeroInicial, entity.UltimoNumeroUsado);
        if (usada && !string.Equals(request.NumeroInicial, entity.NumeroInicial, StringComparison.Ordinal))
        {
            return Result<LineaSerieResponse>.Fallo(new Error(
                "linea_serie.usada.conflicto",
                "La línea ya emitió números: no se puede cambiar el número inicial ni el formato; solo ampliar el final, el aviso, la fecha, el incremento o el bloqueo.",
                "NumeroInicial"));
        }

        // En una línea usada el último usado no se revalida: el inicial no cambia y el final solo crece, así que sigue dentro del
        // rango (y un contador heredado sin relleno, que el motor admite, no bloquea la edición).
        var aviso = CreateLineaSerieCommandHandler.Normalizar(request.NumeroAviso);
        var errores = SerieReglas.ValidarLinea(request.NumeroInicial, request.NumeroFinal, aviso, null, request.Incremento, request.FechaInicial);
        if (errores.Count > 0)
        {
            return Result<LineaSerieResponse>.Fallo([.. errores]);
        }

        if (usada && FormatoNumeroSerie.TryParse(request.NumeroFinal, out var nuevoFinal)
            && FormatoNumeroSerie.TryParse(entity.NumeroFinal, out var finalActual) && nuevoFinal.Valor < finalActual.Valor)
        {
            return Result<LineaSerieResponse>.Fallo(new Error(
                "linea_serie.final_reducido", "En una línea usada el número final solo puede crecer.", "NumeroFinal"));
        }

        if (await LineaSerieReglas.FechaDuplicadaAsync(repositorio, request.SerieId, request.FechaInicial, request.Id, cancellationToken) is { } duplicada)
        {
            return Result<LineaSerieResponse>.Fallo(duplicada);
        }

        // El solapamiento solo se comprueba si cambia el rango: datos heredados ya solapados no impiden bloquear la línea ni cambiar
        // su aviso, fecha o incremento.
        var rangoCambia = !string.Equals(request.NumeroInicial, entity.NumeroInicial, StringComparison.Ordinal)
            || !string.Equals(request.NumeroFinal, entity.NumeroFinal, StringComparison.Ordinal);
        if (rangoCambia)
        {
            await lectura.BloquearTipoAsync(tipo!.Value, cancellationToken);
            var delTipo = await lectura.LineasDelTipoAsync(tipo.Value, cancellationToken);
            if (SerieReglas.Solapada(request.NumeroInicial, request.NumeroFinal, delTipo, request.Id) is { } solapada)
            {
                return Result<LineaSerieResponse>.Fallo(solapada);
            }
        }

        repositorio.EstablecerVersionOriginal(entity, request.Xmin);
        if (!usada && !string.Equals(request.NumeroInicial, entity.NumeroInicial, StringComparison.Ordinal))
        {
            entity.UltimoNumeroUsado = string.Empty;
        }

        entity.NumeroInicial = request.NumeroInicial;
        entity.NumeroFinal = request.NumeroFinal;
        entity.NumeroAviso = aviso;
        entity.FechaInicial = request.FechaInicial;
        entity.Incremento = request.Incremento;
        entity.Bloqueada = request.Bloqueada;
        repositorio.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<LineaSerieResponse>.Exito(await LineaSerieReglas.LeerAsync(lectura, entity, cancellationToken));
    }
}

/// <summary>Borrado lógico de una línea sin usar (una usada se bloquea, no se borra). La serie se bloquea antes que la línea.</summary>
public sealed class DeleteLineaSerieCommandHandler(IUnitOfWork unitOfWork, ISerieReadRepository lectura) : IRequestHandler<DeleteLineaSerieCommand, Result>
{
    public async Task<Result> Handle(DeleteLineaSerieCommand request, CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var repositorio = unitOfWork.Repository<LineaSerie>();
        var entity = await lectura.BloquearSerieAsync(request.SerieId, cancellationToken) is null
            ? null
            : await repositorio.FirstOrDefaultAsync(x => x.Id == request.Id && x.SerieId == request.SerieId, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result.Fallo(SerieReglas.LineaNoEncontrada());
        }

        if (CalculoNumeroSerie.EstaUsada(entity.NumeroInicial, entity.UltimoNumeroUsado))
        {
            return Result.Fallo(new Error("linea_serie.usada.conflicto", "La línea ya emitió números: no se puede eliminar; bloquéela.", "Id"));
        }

        repositorio.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);
        return Result.Exito();
    }
}

internal static class LineaSerieReglas
{
    public static async Task<Error?> FechaDuplicadaAsync(
        IGenericRepository<LineaSerie> repositorio, Guid serieId, DateOnly fecha, Guid? excluir, CancellationToken cancellationToken) =>
        await repositorio.FirstOrDefaultAsync(
            x => x.SerieId == serieId && x.FechaInicial == fecha && x.Id != excluir, cancellationToken: cancellationToken) is null
            ? null
            : new Error("linea_serie.fecha_duplicada", "La serie ya tiene una línea con esa fecha inicial.", "FechaInicial");

    public static async Task<LineaSerieResponse> LeerAsync(ISerieReadRepository lectura, LineaSerie entity, CancellationToken cancellationToken) =>
        (await lectura.GetByIdAsync(entity.SerieId, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken))!.Lineas.Single(l => l.Id == entity.Id);
}
