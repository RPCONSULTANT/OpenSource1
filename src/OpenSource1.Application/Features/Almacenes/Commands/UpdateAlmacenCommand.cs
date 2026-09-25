using MediatR;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Almacenes.Commands;

/// <summary>
/// Modificación de un almacén. <c>Codigo</c> y <c>Nombre</c> son de reemplazo completo (siempre se
/// envían, igual que en UnidadMedida). Los demás campos tienen semántica de MODIFICACIÓN PARCIAL
/// para que un PUT que no los menciona no los pise (igual que SocioNegocio tras el Ruling V):
/// <list type="bullet">
/// <item><c>null</c> (ausente) = conservar el valor actual.</item>
/// <item>Valor informado = se aplica; cadena vacía limpia un campo de dirección opcional.</item>
/// </list>
/// <c>EsPredeterminado = true</c> marca este almacén como predeterminado (desmarca el anterior en
/// la misma transacción); <c>EsPredeterminado = false</c> sobre el predeterminado actual devuelve
/// <c>almacen.predeterminado_requerido</c>: siempre debe existir uno, así que se cambia marcando
/// otro almacén, no desmarcando este.
/// </summary>
public sealed record UpdateAlmacenCommand(
    Guid Id,
    string Codigo,
    string Nombre,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Ciudad,
    string? PaisCodigo,
    bool? Bloqueado,
    bool? EsPredeterminado) : IRequest<Result<AlmacenResponse>>;
