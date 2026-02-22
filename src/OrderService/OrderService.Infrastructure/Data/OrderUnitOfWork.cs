using System.Data;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Data;

public class UnitOfWork(IDbConnectionFactory connectionFactory) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<IDbConnection, IDbTransaction, Task> work)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        await work(connection, transaction);
        transaction.Commit();
        // No explicit rollback needed: disposing an uncommitted transaction automatically rolls it back.
    }

    public async Task<T> ExecuteAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> work)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var result = await work(connection, transaction);
        transaction.Commit();
        // No explicit rollback needed: disposing an uncommitted transaction automatically rolls it back.
        return result;
    }
}
