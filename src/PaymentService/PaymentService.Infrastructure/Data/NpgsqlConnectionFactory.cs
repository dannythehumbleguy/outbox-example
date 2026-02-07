using System.Data;
using Npgsql;

namespace PaymentService.Infrastructure.Data;

public class NpgsqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public IDbConnection CreateConnection() => new NpgsqlConnection(connectionString);
}
