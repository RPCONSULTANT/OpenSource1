using MediatR;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

/// <summary>
/// Alta de socio de negocio y primer consumidor real de <see cref="IGeneradorNumeroDocumento"/>:
/// el <c>Codigo</c> se toma de la serie <see cref="SerieCodigos"/> (sin huecos) dentro de la misma
/// transacción que inserta la fila.
/// </summary>
/// <remarks>
/// Orden deliberado: validación pura (sin BD) -> transacción -> número -> comprobaciones con BD ->
/// insert -> commit. El número se reserva ANTES de las comprobaciones con BD a propósito: el
/// <c>SELECT ... FOR UPDATE</c> de la línea de serie serializa todas las altas, así que la
/// comprobación de documento fiscal duplicado que sigue es libre de carreras entre altas
/// concurrentes (el índice único parcial sigue siendo la red de seguridad final). Cualquier
/// fallo posterior (Result fallido o excepción) sale del <c>await using</c> sin commit, lo que hace
/// rollback de la transacción y deja el contador de la serie SIN consumir: no hay huecos.
/// </remarks>
public sealed class CreateSocioNegocioCommandHandler(IUnitOfWork unitOfWork, IGeneradorNumeroDocumento generadorNumero)
    : IRequestHandler<CreateSocioNegocioCommand, Result<SocioNegocioResponse>>
{
    public const string SerieCodigos = "SOCIOS";

    public async Task<Result<SocioNegocioResponse>> Handle(CreateSocioNegocioCommand request, CancellationToken cancellationToken)
    {
        var errores = SocioNegocioValidator.Validar(request);
        if (errores.Count > 0)
        {
            return Result<SocioNegocioResponse>.Fallo([.. errores]);
        }

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var numero = await generadorNumero.SiguienteAsync(
            SerieCodigos, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        if (!numero.TryObtenerValor(out var codigo))
        {
            return Result<SocioNegocioResponse>.Fallo(numero);
        }

        var verificacion = await SocioNegocioReglas.VerificarReferenciasAsync(unitOfWork, request, idActual: null, validarTermino: true, cancellationToken);
        if (verificacion is not null)
        {
            return Result<SocioNegocioResponse>.Fallo(verificacion.Value);
        }

        var resolucionGrupos = await SocioNegocioReglas.ResolverGruposAsync(unitOfWork, request, actual: null, cancellationToken);
        if (!resolucionGrupos.TryObtenerValor(out var grupos))
        {
            return Result<SocioNegocioResponse>.Fallo(resolucionGrupos);
        }

        var entity = new SocioNegocio
        {
            Codigo = codigo,
            NombreComercial = request.NombreComercial.Trim()
        };
        SocioNegocioReglas.Aplicar(entity, request);

        await unitOfWork.Repository<SocioNegocio>().AddAsync(entity, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<SocioNegocioResponse>.Exito(ToResponse(entity, grupos));
    }

    public static SocioNegocioResponse ToResponse(SocioNegocio x, SocioNegocioGruposContables? grupos = null) => new()
    {
        Id = x.Id,
        Codigo = x.Codigo,
        Tipo = x.Tipo,
        NombreComercial = x.NombreComercial,
        RazonSocial = x.RazonSocial,
        TipoDocumentoFiscal = x.TipoDocumentoFiscal,
        NumeroDocumentoFiscal = x.NumeroDocumentoFiscal,
        Email = x.Email,
        Telefono = x.Telefono,
        DireccionLinea1 = x.Direccion?.Linea1,
        DireccionLinea2 = x.Direccion?.Linea2,
        Ciudad = x.Ciudad,
        Sector = x.Sector?.Nombre,
        PaisCodigo = x.Pais?.Codigo,
        PaisNombre = x.Pais?.Nombre,
        TerminoPagoId = x.TerminoPagoId,
        LimiteCredito = x.LimiteCredito,
        Bloqueado = x.Bloqueado,
        ImagePath = x.ImagePath,
        GrupoNegocioId = x.GrupoNegocioId,
        GrupoNegocioCodigo = grupos?.Negocio?.Codigo,
        GrupoIvaNegocioId = x.GrupoIvaNegocioId,
        GrupoIvaNegocioCodigo = grupos?.IvaNegocio?.Codigo,
        GrupoClienteContableId = x.GrupoClienteContableId,
        GrupoClienteContableCodigo = grupos?.ClienteContable?.Codigo,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}

/// <summary>Grupos contables resueltos del socio (null = sin grupo, o grupo colgante que no cambió).</summary>
public sealed record SocioNegocioGruposContables(GrupoNegocio? Negocio, GrupoIvaNegocio? IvaNegocio, GrupoClienteContable? ClienteContable);

/// <summary>Reglas compartidas por Create/Update que tocan la entidad o la base de datos.</summary>
internal static class SocioNegocioReglas
{
    /// <summary>
    /// Resuelve los tres grupos contables (Task 5.3). Un grupo que se ASIGNA o CAMBIA (distinto del de <paramref name="actual"/>)
    /// debe existir y no estar borrado: si no, 400 <c>socio_negocio.grupo_invalido</c> en su campo. Uno que no cambia no se
    /// revalida (mismo criterio que el término de pago).
    /// </summary>
    public static async Task<Result<SocioNegocioGruposContables>> ResolverGruposAsync(
        IUnitOfWork unitOfWork, IDatosSocioNegocio datos, SocioNegocio? actual, CancellationToken cancellationToken)
    {
        var negocio = await BuscarGrupoAsync<GrupoNegocio>(unitOfWork, datos.GrupoNegocioId, cancellationToken);
        if (negocio is null && datos.GrupoNegocioId is not null && datos.GrupoNegocioId != actual?.GrupoNegocioId)
        {
            return Result<SocioNegocioGruposContables>.Fallo(ErrorGrupo("El grupo de negocio indicado no existe.", nameof(datos.GrupoNegocioId)));
        }

        var ivaNegocio = await BuscarGrupoAsync<GrupoIvaNegocio>(unitOfWork, datos.GrupoIvaNegocioId, cancellationToken);
        if (ivaNegocio is null && datos.GrupoIvaNegocioId is not null && datos.GrupoIvaNegocioId != actual?.GrupoIvaNegocioId)
        {
            return Result<SocioNegocioGruposContables>.Fallo(ErrorGrupo("El grupo de IVA de negocio indicado no existe.", nameof(datos.GrupoIvaNegocioId)));
        }

        var clienteContable = await BuscarGrupoAsync<GrupoClienteContable>(unitOfWork, datos.GrupoClienteContableId, cancellationToken);
        if (clienteContable is null && datos.GrupoClienteContableId is not null && datos.GrupoClienteContableId != actual?.GrupoClienteContableId)
        {
            return Result<SocioNegocioGruposContables>.Fallo(ErrorGrupo("El grupo contable de cliente indicado no existe.", nameof(datos.GrupoClienteContableId)));
        }

        return Result<SocioNegocioGruposContables>.Exito(new SocioNegocioGruposContables(negocio, ivaNegocio, clienteContable));
    }

    private static Task<TGrupo?> BuscarGrupoAsync<TGrupo>(IUnitOfWork unitOfWork, Guid? id, CancellationToken cancellationToken)
        where TGrupo : GrupoContable =>
        id is { } grupoId
            ? unitOfWork.Repository<TGrupo>().FirstOrDefaultAsync(x => x.Id == grupoId, cancellationToken: cancellationToken)
            : Task.FromResult<TGrupo?>(null);

    // Sin sufijo ".no_encontrado" a propósito: es un dato inválido del cuerpo (400), no el recurso de la URL (404).
    private static Error ErrorGrupo(string mensaje, string campo) => new("socio_negocio.grupo_invalido", mensaje, campo);

    /// <summary>
    /// Comprueba lo que exige base de datos: <c>TerminoPagoId</c> (si viene) existe y no está
    /// borrado (solo si <paramref name="validarTermino"/>: en una modificación que no lo cambia no se
    /// revalida); el documento fiscal (si viene) no lo usa otro socio no borrado. Devuelve el error
    /// o <c>null</c>. El filtro global de EF excluye las filas borradas lógicamente.
    /// </summary>
    public static async Task<Error?> VerificarReferenciasAsync(
        IUnitOfWork unitOfWork, IDatosSocioNegocio datos, Guid? idActual, bool validarTermino, CancellationToken cancellationToken)
    {
        if (validarTermino && datos.TerminoPagoId is { } terminoPagoId)
        {
            // FirstOrDefaultAsync (consulta) y no GetByIdAsync (Find): solo la consulta aplica el filtro
            // global de soft delete, y un término de pago borrado no es una referencia válida.
            var termino = await unitOfWork.Repository<TerminoPago>().FirstOrDefaultAsync(
                x => x.Id == terminoPagoId, cancellationToken: cancellationToken);
            if (termino is null)
            {
                // Código sin sufijo ".no_encontrado" a propósito: es un dato inválido del cuerpo (400),
                // no un recurso de la URL inexistente (404).
                return new Error(
                    "socio_negocio.termino_pago_invalido",
                    "El término de pago indicado no existe.",
                    nameof(datos.TerminoPagoId));
            }
        }

        // Una imagen solo puede pertenecer a UN socio: si otro registro pudiera apuntar al fichero de este, al sustituir
        // su imagen la UI borraría la ajena. (Un fichero sin dueño es un huérfano, no una imagen de otro socio.)
        // LIMITACIÓN CONOCIDA: consulta previa SIN índice único; dos peticiones concurrentes con el mismo imagePath pueden asignarlo
        // a dos registros (medido: 40 de 40 rondas). Consecuencia: al sustituir la imagen de uno se borra el fichero y el otro queda
        // con la imagen rota; NO es explotable para borrar el fichero de una víctima. Corrección de raíz recomendada (no implementada):
        // índice único parcial sobre ImagePath (no nulo y no borrado) o comprobar referencias antes de borrar el fichero.
        if (!string.IsNullOrEmpty(datos.ImagePath))
        {
            var imagenEnUso = await unitOfWork.Repository<SocioNegocio>().FirstOrDefaultAsync(
                x => x.ImagePath == datos.ImagePath && (idActual == null || x.Id != idActual),
                cancellationToken: cancellationToken);
            if (imagenEnUso is not null)
            {
                return new Error(
                    "socio_negocio.imagen_en_uso",
                    "La imagen ya está asignada a otro socio de negocio.",
                    nameof(datos.ImagePath));
            }
        }

        var documento = NormalizarDocumento(datos.NumeroDocumentoFiscal);
        if (documento is not null)
        {
            var duplicado = await unitOfWork.Repository<SocioNegocio>().FirstOrDefaultAsync(
                x => x.NumeroDocumentoFiscal == documento && (idActual == null || x.Id != idActual),
                cancellationToken: cancellationToken);
            if (duplicado is not null)
            {
                return new Error(
                    "socio_negocio.conflicto",
                    "Ya existe un socio de negocio con ese número de documento fiscal.",
                    nameof(datos.NumeroDocumentoFiscal));
            }
        }

        return null;
    }

    /// <summary>Copia los datos editables (todo salvo <c>Codigo</c>) a la entidad.</summary>
    public static void Aplicar(SocioNegocio entity, IDatosSocioNegocio datos)
    {
        entity.NombreComercial = datos.NombreComercial.Trim();
        entity.RazonSocial = Normalizar(datos.RazonSocial);
        entity.Tipo = datos.Tipo;
        entity.TipoDocumentoFiscal = datos.TipoDocumentoFiscal;
        entity.NumeroDocumentoFiscal = NormalizarDocumento(datos.NumeroDocumentoFiscal);
        entity.Email = Normalizar(datos.Email);
        entity.Telefono = Normalizar(datos.Telefono);
        entity.Direccion = string.IsNullOrWhiteSpace(datos.DireccionLinea1)
            ? null
            : DireccionFiscal.Of(datos.DireccionLinea1, datos.DireccionLinea2, nameof(datos.DireccionLinea1));
        entity.Ciudad = Normalizar(datos.Ciudad);
        entity.Sector = string.IsNullOrWhiteSpace(datos.Sector) ? null : Sector.Of(datos.Sector, nameof(datos.Sector));
        entity.Pais = string.IsNullOrWhiteSpace(datos.PaisCodigo) ? null : Pais.Of(datos.PaisCodigo, nameof(datos.PaisCodigo));
        entity.TerminoPagoId = datos.TerminoPagoId;
        entity.LimiteCredito = datos.LimiteCredito;
        entity.Bloqueado = datos.Bloqueado;
        entity.ImagePath = datos.ImagePath;
        entity.GrupoNegocioId = datos.GrupoNegocioId;
        entity.GrupoIvaNegocioId = datos.GrupoIvaNegocioId;
        entity.GrupoClienteContableId = datos.GrupoClienteContableId;
    }

    /// <summary>Sin espacios sobrantes y en mayúsculas: <c>abc123</c> y <c>ABC123</c> son el mismo documento.</summary>
    private static string? NormalizarDocumento(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
