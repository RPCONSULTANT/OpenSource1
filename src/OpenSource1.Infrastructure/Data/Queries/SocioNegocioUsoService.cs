using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.SociosNegocio;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Guarda de uso de socios de negocio (Task 6.4). En uso si es vender-a o facturar-a de un borrador de factura NO borrado, o si
/// aparece en una factura posteada (vender-a o facturar-a), en el libro de clientes, en el libro contable o en el libro de
/// inventario (los libros y el documento posteado no tienen borrado lógico).
/// </summary>
public sealed class SocioNegocioUsoService(IDbSession session) : ISocioNegocioUsoService
{
    private const string Sql = """
        SELECT EXISTS (
            SELECT 1 FROM "FacturasVentaBorrador"
            WHERE "IsDeleted" = false AND ("SocioNegocioId" = @Id OR "SocioNegocioFacturarAId" = @Id)
        ) OR EXISTS (
            SELECT 1 FROM "FacturasVenta" WHERE "SocioNegocioId" = @Id
        ) OR EXISTS (
            SELECT 1 FROM "FacturasVenta" WHERE "SocioNegocioFacturarAId" = @Id
        ) OR EXISTS (
            SELECT 1 FROM "MovimientosCliente" WHERE "SocioNegocioId" = @Id
        ) OR EXISTS (
            SELECT 1 FROM "MovimientosContables" WHERE "SocioNegocioId" = @Id
        ) OR EXISTS (
            SELECT 1 FROM "MovimientosProducto" WHERE "SocioNegocioId" = @Id
        )
        """;

    public async Task<bool> EstaEnUsoAsync(Guid socioNegocioId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(Sql, new { Id = socioNegocioId }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task BloquearAsync(Guid socioNegocioId, CancellationToken cancellationToken = default)
    {
        if (!session.HayTransaccionActiva)
        {
            throw new InvalidOperationException("Bloquear un socio de negocio requiere una transacción activa.");
        }

        await session.EnsureOpenAsync(cancellationToken);
        await session.Connection.ExecuteAsync(new CommandDefinition(
            """SELECT 1 FROM "SociosNegocio" WHERE "Id" = @Id FOR UPDATE""",
            new { Id = socioNegocioId }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
