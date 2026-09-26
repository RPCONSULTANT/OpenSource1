using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.FacturasVenta.Borradores;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class FacturaVentaBorradorDatos(IDbSession session) : IFacturaVentaBorradorDatos
{
    public async Task<int> BorrarLineasAsync(Guid facturaVentaBorradorId, string usuario, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Borrar las líneas de un borrador de factura requiere una transacción activa.");
        }

        // Mismas columnas que el borrado lógico de UnitOfWork (IsDeleted/DeletedAtUtc/DeletedBy); xmin cambia solo.
        const string sql = """
            UPDATE "LineasFacturaVentaBorrador" SET "IsDeleted" = true, "DeletedAtUtc" = @Ahora, "DeletedBy" = @Usuario
            WHERE "FacturaVentaBorradorId" = @Id AND "IsDeleted" = false
            """;

        return await session.Connection.ExecuteAsync(new CommandDefinition(
            sql, new { Id = facturaVentaBorradorId, Ahora = DateTimeOffset.UtcNow, Usuario = usuario },
            session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
