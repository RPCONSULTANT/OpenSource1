using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Guarda de uso de cuentas contables. Task 5.3: una cuenta está en uso si la referencia (como CxC, descuento o interés) un
/// grupo contable de cliente NO borrado lógicamente. Las Tasks 5.4-5.5 añaden aquí los setups contables y los movimientos del
/// libro contable (otra condición en el mismo <c>EXISTS</c> o una consulta más), sin tocar a los llamadores.
/// </summary>
public sealed class CuentaContableUsoService(IDbSession session) : ICuentaContableUsoService
{
    private const string Sql = """
        SELECT EXISTS (
            SELECT 1 FROM "GruposClienteContable"
            WHERE "IsDeleted" = false
              AND ("CuentaCxCId" = @Id OR "CuentaDescuentoId" = @Id OR "CuentaInteresId" = @Id)
        )
        """;

    public async Task<bool> EstaEnUsoAsync(Guid cuentaContableId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(Sql, new { Id = cuentaContableId }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
