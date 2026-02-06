using System.Data;

namespace OrderService.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
