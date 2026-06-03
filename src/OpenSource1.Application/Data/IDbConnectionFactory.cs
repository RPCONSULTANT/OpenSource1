using System.Data;

namespace OpenSource1.Application.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
