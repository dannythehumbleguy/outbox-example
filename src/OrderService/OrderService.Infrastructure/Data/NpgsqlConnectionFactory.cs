using System.Data;
using Npgsql;

namespace OrderService.Infrastructure.Data;

public class NpgsqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public IDbConnection CreateConnection() => new NpgsqlConnection(connectionString);
}
