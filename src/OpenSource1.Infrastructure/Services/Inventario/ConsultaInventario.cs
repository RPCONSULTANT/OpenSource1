using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Inventario;

namespace OpenSource1.Infrastructure.Services.Inventario;

/// <summary>
/// Consultas del libro con Dapper sobre la conexión de <see cref="IDbSession"/>: dentro de una transacción ve lo que ella
/// misma ha escrito (el registro la usa para validar existencia y calcular el costo con sus propias filas).
/// </summary>
public sealed class ConsultaInventario(IDbSession session) : IConsultaInventario
{
    public async Task<decimal> ExistenciaAsync(Guid productoId, Guid? almacenId, DateOnly? fecha, CancellationToken ct = default)
    {
        await session.EnsureOpenAsync(ct);

        var sql = """SELECT COALESCE(SUM("Cantidad"), 0) FROM "MovimientosProducto" WHERE "ProductoId" = @productoId""";
        var parametros = new DynamicParameters(new { productoId });
        if (almacenId is not null)
        {
            sql += """ AND "AlmacenId" = @almacenId""";
            parametros.Add("almacenId", almacenId.Value);
        }

        if (fecha is not null)
        {
            sql += """ AND "FechaRegistro" <= @fecha""";
            parametros.Add("fecha", fecha.Value);
        }

        return await session.Connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sql, parametros, session.CurrentTransaction, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ExistenciaAlmacen>> ExistenciasPorAlmacenAsync(Guid productoId, CancellationToken ct = default)
    {
        await session.EnsureOpenAsync(ct);

        // Sin filtro de borrado en Almacenes: un almacén con movimientos no se puede borrar (409), y si llegara a estarlo
        // su existencia debe seguir viéndose.
        const string sql = """
            SELECT a."Id" AS "AlmacenId", a."Codigo" AS "AlmacenCodigo", a."Nombre" AS "AlmacenNombre",
                   SUM(m."Cantidad") AS "Existencia"
            FROM "MovimientosProducto" m
            JOIN "Almacenes" a ON a."Id" = m."AlmacenId"
            WHERE m."ProductoId" = @productoId
            GROUP BY a."Id", a."Codigo", a."Nombre"
            ORDER BY a."Codigo"
            """;
        var filas = await session.Connection.QueryAsync<ExistenciaAlmacen>(
            new CommandDefinition(sql, new { productoId }, session.CurrentTransaction, cancellationToken: ct));
        return filas.ToList();
    }

    public async Task<decimal?> CostoPromedioAsync(Guid productoId, DateOnly fecha, CancellationToken ct = default)
    {
        await session.EnsureOpenAsync(ct);

        var (v, q) = await session.Connection.QuerySingleAsync<(decimal V, decimal Q)>(
            new CommandDefinition(CostoPromedioCalculadora.SumasSql, new { productoId, fecha }, session.CurrentTransaction, cancellationToken: ct));
        return CostoPromedioCalculadora.Calcular(v, q);
    }
}
