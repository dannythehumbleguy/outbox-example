using System.Data;

namespace OrderService.Application.Interfaces;

public interface IUnitOfWork
{
    Task ExecuteAsync(Func<IDbConnection, IDbTransaction, Task> work);
    Task<T> ExecuteAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> work);
}
