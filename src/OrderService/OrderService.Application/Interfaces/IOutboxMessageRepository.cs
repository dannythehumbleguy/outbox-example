using System.Data;

namespace OrderService.Application.Interfaces;

public interface IOutboxMessageRepository
{
    Task CreateAsync<T>(T payload, IDbConnection connection, IDbTransaction transaction) where T : class;
}
