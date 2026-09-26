using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Implementación de la Task 5.3: un grupo está en uso si lo referencia un producto o un socio de negocio NO borrado
/// lógicamente (los índices de las FK <c>IX_Productos_Grupo*</c>/<c>IX_SociosNegocio_Grupo*</c> sirven a estas consultas). La
/// Task 5.4 añade aquí las referencias desde los setups contables. Consulta Dapper contra la sesión del scope (ve la
/// transacción del llamador si la hay).
/// </summary>
public sealed class GrupoContableUsoService(IDbSession session) : IGrupoContableUsoService
{
    public Task<bool> EstaEnUsoAsync(TipoGrupoContable tipo, Guid grupoId, CancellationToken cancellationToken = default)
    {
        // Tabla y columna salen de este switch sobre el enum (literales de código), nunca de texto del usuario.
        var (tabla, columna) = tipo switch
        {
            TipoGrupoContable.Negocio => ("SociosNegocio", "GrupoNegocioId"),
            TipoGrupoContable.IvaNegocio => ("SociosNegocio", "GrupoIvaNegocioId"),
            TipoGrupoContable.Producto => ("Productos", "GrupoProductoId"),
            TipoGrupoContable.IvaProducto => ("Productos", "GrupoIvaProductoId"),
            TipoGrupoContable.Inventario => ("Productos", "GrupoInventarioId"),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de grupo contable desconocido.")
        };

        return ExisteAsync(
            $"""SELECT EXISTS (SELECT 1 FROM "{tabla}" WHERE "{columna}" = @Id AND "IsDeleted" = false)""",
            grupoId, cancellationToken);
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
