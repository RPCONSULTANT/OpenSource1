using System.Data;
using Dapper;

namespace OpenSource1.Infrastructure.Data;

/// <summary>
/// Dapper (a diferencia del proveedor Npgsql de EF Core, que mapea <see cref="DateOnly"/> a
/// <c>date</c> de forma nativa) no sabe inferir el <see cref="DbType"/> de un parámetro
/// <see cref="DateOnly"/>: sin este handler, <c>SqlMapper.LookupDbType</c> lanza
/// <see cref="NotSupportedException"/> al primer intento de ejecutar una consulta con un
/// parámetro <see cref="DateOnly"/> (encontrado y verificado empíricamente contra Postgres real
/// al escribir <c>GeneradorNumeroDocumentoTests</c>).
/// </summary>
/// <remarks>
/// Se registra una sola vez, a nivel de proceso, desde
/// <see cref="DependencyInjection.AddApplicationData"/> — no ligado al constructor estático de
/// una clase de negocio concreta como <c>GeneradorNumeroDocumento</c>, que solo se ejecutaría la
/// primera vez que esa clase puntual se resuelve por DI. Registrarlo aquí garantiza que
/// cualquier repositorio Dapper que use <see cref="DateOnly"/> como parámetro, sin importar cuál
/// se resuelva primero, ya lo tenga disponible.
/// </remarks>
public sealed class DapperDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);
}
