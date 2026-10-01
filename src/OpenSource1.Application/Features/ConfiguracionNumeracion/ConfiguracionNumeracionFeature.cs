using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Application.Features.Series;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.ConfiguracionNumeracion;

public sealed record ListConfiguracionNumeracionQuery : IRequest<Result<IReadOnlyList<ConfiguracionNumeracionResponse>>>;

public sealed record UpdateConfiguracionNumeracionCommand(TipoDocumentoSerie Tipo, Guid SerieId, long Xmin)
    : IRequest<Result<ConfiguracionNumeracionResponse>>;

public sealed class ListConfiguracionNumeracionQueryHandler(IConfiguracionNumeracionReadRepository lectura)
    : IRequestHandler<ListConfiguracionNumeracionQuery, Result<IReadOnlyList<ConfiguracionNumeracionResponse>>>
{
    public async Task<Result<IReadOnlyList<ConfiguracionNumeracionResponse>>> Handle(
        ListConfiguracionNumeracionQuery request, CancellationToken cancellationToken) =>
        Result<IReadOnlyList<ConfiguracionNumeracionResponse>>.Exito(await lectura.ListAsync(cancellationToken));
}

/// <summary>
/// Cambia la serie predeterminada de un tipo (CanAdministrar, <c>xmin</c>). La serie debe existir, estar activa y numerar ese tipo.
/// No afecta a documentos ya creados: los borradores guardan sus series. Sin fila de configuración, 404 sin bloquear nada; con
/// ella, la serie nueva se bloquea <c>FOR UPDATE</c> antes de comprobarla y de escribir la configuración (Series antes que
/// cualquier otra fila: mismo orden que el motor y que la administración de series). Así una desactivación concurrente de esa serie espera y, al verla asignada, se rechaza (NS5); si la
/// desactivación va primero, aquí se ve la serie inactiva.
/// </summary>
public sealed class UpdateConfiguracionNumeracionCommandHandler(
    IUnitOfWork unitOfWork, IConfiguracionNumeracionReadRepository lectura, ISerieReadRepository series)
    : IRequestHandler<UpdateConfiguracionNumeracionCommand, Result<ConfiguracionNumeracionResponse>>
{
    public async Task<Result<ConfiguracionNumeracionResponse>> Handle(
        UpdateConfiguracionNumeracionCommand request, CancellationToken cancellationToken)
    {
        if (!TipoDocumentoSerieNombres.EsValido(request.Tipo))
        {
            return Result<ConfiguracionNumeracionResponse>.Fallo(new Error(
                "configuracion_numeracion.tipo_invalido", "El tipo de documento no existe.", "TipoDocumento"));
        }

        // Sin CommitAsync, salir del "await using" deshace la transacción.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // La fila se lee (sin bloquearla) antes de bloquear la serie: sin configuración no se bloquea nada. El orden de bloqueos no
        // cambia: la serie FOR UPDATE y después la escritura de la configuración.
        var repositorio = unitOfWork.Repository<Core.Entities.ConfiguracionNumeracion>();
        var entity = await repositorio.FirstOrDefaultAsync(x => x.TipoDocumento == request.Tipo, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<ConfiguracionNumeracionResponse>.Fallo(new Error(
                "configuracion_numeracion.no_encontrado", "No hay configuración para ese tipo de documento.", "TipoDocumento"));
        }

        var tipoSerie = await series.BloquearSerieAsync(request.SerieId, cancellationToken);
        var serie = tipoSerie == request.Tipo
            ? await unitOfWork.Repository<Serie>().FirstOrDefaultAsync(x => x.Id == request.SerieId, cancellationToken: cancellationToken)
            : null;
        if (serie is not { Activa: true })
        {
            return Result<ConfiguracionNumeracionResponse>.Fallo(new Error(
                "configuracion_numeracion.serie_invalida",
                $"La serie debe existir, estar activa y numerar {TipoDocumentoSerieNombres.Nombre(request.Tipo)}.",
                "SerieId"));
        }

        repositorio.EstablecerVersionOriginal(entity, request.Xmin);
        entity.SerieId = request.SerieId;
        repositorio.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<ConfiguracionNumeracionResponse>.Exito(
            (await lectura.ListAsync(cancellationToken)).Single(x => x.TipoDocumento == request.Tipo));
    }
}
