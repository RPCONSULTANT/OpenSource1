using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

/// <summary>
/// Modificación de un socio de negocio. <c>Codigo</c> es inmutable: el comando no lo lleva y
/// <see cref="SocioNegocioReglas.Aplicar"/> nunca lo toca. Los campos de la 2.6 se modifican de
/// forma parcial (ver <see cref="UpdateSocioNegocioCommand"/>): se resuelven contra los valores
/// guardados antes de validar, de modo que las reglas cruzadas (tipo de documento vs número) se
/// evalúan sobre el resultado final.
/// </summary>
public sealed class UpdateSocioNegocioCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateSocioNegocioCommand, Result<SocioNegocioResponse>>
{
    public async Task<Result<SocioNegocioResponse>> Handle(UpdateSocioNegocioCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<SocioNegocio>();

        // Consulta con seguimiento (no Find): defensa en profundidad. Find devolvería una entidad ya
        // rastreada en el mismo scope aunque esté borrada lógicamente; la consulta siempre pasa por
        // el filtro global de soft delete. (Entre peticiones, con un scope por petición, ambas vías
        // dan el mismo resultado.)
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<SocioNegocioResponse>.Fallo(new Error(
                "socio_negocio.no_encontrado", "No se encontró el socio de negocio solicitado.", "Id"));
        }

        var datos = DatosResueltos.De(request, entity);

        var errores = SocioNegocioValidator.Validar(datos);
        if (errores.Count > 0)
        {
            return Result<SocioNegocioResponse>.Fallo([.. errores]);
        }

        // El término de pago solo se revalida si CAMBIA: un socio con una referencia ya colgante
        // (dato previo a que el borrado de términos en uso se bloqueara) debe seguir siendo editable.
        var terminoCambio = datos.TerminoPagoId != entity.TerminoPagoId;
        var verificacion = await SocioNegocioReglas.VerificarReferenciasAsync(
            unitOfWork, datos, request.Id, validarTermino: terminoCambio, cancellationToken);
        if (verificacion is not null)
        {
            return Result<SocioNegocioResponse>.Fallo(verificacion.Value);
        }

        var resolucionGrupos = await SocioNegocioReglas.ResolverGruposAsync(unitOfWork, datos, entity, cancellationToken);
        if (!resolucionGrupos.TryObtenerValor(out var grupos))
        {
            return Result<SocioNegocioResponse>.Fallo(resolucionGrupos);
        }

        SocioNegocioReglas.Aplicar(entity, datos);
        SocioNegocioReglas.AplicarGrupos(entity, grupos);

        repo.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SocioNegocioResponse>.Exito(CreateSocioNegocioCommandHandler.ToResponse(entity, grupos));
    }

    /// <summary>Comando + valores guardados = datos finales del socio tras la modificación.</summary>
    private sealed record DatosResueltos(
        string NombreComercial,
        string? RazonSocial,
        TipoSocioNegocio Tipo,
        TipoDocumentoFiscal TipoDocumentoFiscal,
        string? NumeroDocumentoFiscal,
        string? Email,
        string? Telefono,
        string? DireccionLinea1,
        string? DireccionLinea2,
        string? Ciudad,
        string? Sector,
        string? PaisCodigo,
        Guid? TerminoPagoId,
        decimal LimiteCredito,
        BloqueoSocioNegocio Bloqueado,
        string? ImagePath,
        Guid? GrupoNegocioId,
        Guid? GrupoIvaNegocioId,
        Guid? GrupoClienteContableId) : IDatosSocioNegocio
    {
        public static DatosResueltos De(UpdateSocioNegocioCommand c, SocioNegocio actual) => new(
            c.NombreComercial,
            c.RazonSocial ?? actual.RazonSocial,
            c.Tipo ?? actual.Tipo,
            c.TipoDocumentoFiscal ?? actual.TipoDocumentoFiscal,
            c.NumeroDocumentoFiscal ?? actual.NumeroDocumentoFiscal,
            c.Email,
            c.Telefono,
            c.DireccionLinea1,
            c.DireccionLinea2,
            c.Ciudad ?? actual.Ciudad,
            c.Sector,
            c.PaisCodigo,
            // Guid.Empty = limpiar el término de pago; null = conservar.
            c.TerminoPagoId is null ? actual.TerminoPagoId : (c.TerminoPagoId == Guid.Empty ? null : c.TerminoPagoId),
            c.LimiteCredito ?? actual.LimiteCredito,
            c.Bloqueado ?? actual.Bloqueado,
            c.ImagePath,
            // Grupos contables (Task 5.3): null = conservar; no hay "limpiar".
            c.GrupoNegocioId ?? actual.GrupoNegocioId,
            c.GrupoIvaNegocioId ?? actual.GrupoIvaNegocioId,
            c.GrupoClienteContableId ?? actual.GrupoClienteContableId);
    }
}
