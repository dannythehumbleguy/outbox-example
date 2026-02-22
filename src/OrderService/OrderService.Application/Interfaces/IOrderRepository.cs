using System.Data;
using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order> CreateAsync(Order order, IDbConnection connection, IDbTransaction transaction);
}
