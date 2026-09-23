using System.Data.Common;
using LibraSys.Api.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace LibraSys.Api.Data;

public sealed class MySqlConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public DbConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "Database:ConnectionString is not configured. Use user secrets or an environment variable.");
        }

        return new MySqlConnection(_connectionString);
    }
}
