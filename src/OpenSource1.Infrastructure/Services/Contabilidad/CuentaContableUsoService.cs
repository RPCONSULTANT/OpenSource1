using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Guarda de uso de cuentas contables. Una cuenta está en uso si la referencia, en cualquiera de sus columnas de cuenta, una fila
/// NO borrada lógicamente de: un grupo contable de cliente (CxC, descuento o interés; Task 5.3) o uno de los tres setups
/// contables (Task 5.4), o si tiene algún movimiento en el libro contable (Task 5.5; el libro no tiene borrado lógico y usa el
/// índice <c>IX_MovimientosContables_CuentaContableId_FechaRegistro</c>).
/// </summary>
public sealed class CuentaContableUsoService(IDbSession session) : ICuentaContableUsoService
{
    private const string Sql = """
        SELECT EXISTS (
            SELECT 1 FROM "MovimientosContables" WHERE "CuentaContableId" = @Id
        ) OR EXISTS (
            SELECT 1 FROM "GruposClienteContable"
            WHERE "IsDeleted" = false
              AND ("CuentaCxCId" = @Id OR "CuentaDescuentoId" = @Id OR "CuentaInteresId" = @Id)
        ) OR EXISTS (
            SELECT 1 FROM "SetupsContableGeneral"
            WHERE "IsDeleted" = false
              AND ("CuentaVentasId" = @Id OR "CuentaCostoVentasId" = @Id OR "CuentaDescuentoVentasId" = @Id OR "CuentaAjusteInventarioId" = @Id)
        ) OR EXISTS (
            SELECT 1 FROM "SetupsIva"
            WHERE "IsDeleted" = false AND ("CuentaIvaVentasId" = @Id OR "CuentaIvaComprasId" = @Id)
        ) OR EXISTS (
            SELECT 1 FROM "SetupsInventario"
            WHERE "IsDeleted" = false
              AND ("CuentaInventarioId" = @Id OR "CuentaAjusteInventarioId" = @Id OR "CuentaVariacionCostoId" = @Id)
        )
        """;

    public async Task<bool> EstaEnUsoAsync(Guid cuentaContableId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(Sql, new { Id = cuentaContableId }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
