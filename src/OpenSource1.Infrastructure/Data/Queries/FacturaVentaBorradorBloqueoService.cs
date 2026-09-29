using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.FacturasVenta.Borradores;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>Análogo a <see cref="LoteDiarioBloqueoService"/> para los borradores de factura.</summary>
public sealed class FacturaVentaBorradorBloqueoService(IDbSession session) : IFacturaVentaBorradorBloqueoService
{
    public async Task<EstadoFacturaBorrador?> BloquearYObtenerEstadoAsync(Guid facturaVentaBorradorId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);

        // FOR UPDATE sin transacción no retiene el bloqueo más allá del statement (mismo motivo que GeneradorNumeroDocumento).
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Bloquear un borrador de factura requiere una transacción activa.");
        }

        const string sql = """
            SELECT "Estado" FROM "FacturasVentaBorrador" WHERE "Id" = @Id AND "IsDeleted" = false FOR UPDATE
            """;

        var estado = await session.Connection.QuerySingleOrDefaultAsync<short?>(
            new CommandDefinition(sql, new { Id = facturaVentaBorradorId }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return estado is { } valor ? (EstadoFacturaBorrador)valor : null;
    }
}
