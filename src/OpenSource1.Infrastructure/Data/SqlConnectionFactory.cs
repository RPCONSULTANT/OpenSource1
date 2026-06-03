using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using OpenSource1.Application.Data;

namespace OpenSource1.Infrastructure.Data;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
