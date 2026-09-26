using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Guarda de uso de los grupos contables: un grupo está en uso si lo referencia un producto o un socio de negocio (Task 5.3) o una
/// fila de setup contable (Task 5.4, en cualquiera de sus dos ejes), todos NO borrados lógicamente. Los índices de las FK sirven a
/// estas consultas. Consulta Dapper contra la sesión del scope (ve la transacción del llamador si la hay).
/// </summary>
public sealed class GrupoContableUsoService(IDbSession session) : IGrupoContableUsoService
{
    public Task<bool> EstaEnUsoAsync(TipoGrupoContable tipo, Guid grupoId, CancellationToken cancellationToken = default)
    {
        // Tablas y columnas salen de este switch sobre el enum (literales de código), nunca de texto del usuario.
        (string Tabla, string Columna)[] referencias = tipo switch
        {
            TipoGrupoContable.Negocio => [("SociosNegocio", "GrupoNegocioId"), ("SetupsContableGeneral", "GrupoNegocioId")],
            TipoGrupoContable.IvaNegocio => [("SociosNegocio", "GrupoIvaNegocioId"), ("SetupsIva", "GrupoIvaNegocioId")],
            TipoGrupoContable.Producto => [("Productos", "GrupoProductoId"), ("SetupsContableGeneral", "GrupoProductoId")],
            TipoGrupoContable.IvaProducto => [("Productos", "GrupoIvaProductoId"), ("SetupsIva", "GrupoIvaProductoId")],
            TipoGrupoContable.Inventario => [("Productos", "GrupoInventarioId"), ("SetupsInventario", "GrupoInventarioId")],
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de grupo contable desconocido.")
        };

        var sql = "SELECT " + string.Join(" OR ", referencias.Select(r =>
            $"""EXISTS (SELECT 1 FROM "{r.Tabla}" WHERE "{r.Columna}" = @Id AND "IsDeleted" = false)"""));
        return ExisteAsync(sql, grupoId, cancellationToken);
    }

    public Task<bool> GrupoClienteContableEnUsoAsync(Guid grupoId, CancellationToken cancellationToken = default) =>
        ExisteAsync(
            """SELECT EXISTS (SELECT 1 FROM "SociosNegocio" WHERE "GrupoClienteContableId" = @Id AND "IsDeleted" = false)""",
            grupoId, cancellationToken);

    private async Task<bool> ExisteAsync(string sql, Guid id, CancellationToken cancellationToken)
    {
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
