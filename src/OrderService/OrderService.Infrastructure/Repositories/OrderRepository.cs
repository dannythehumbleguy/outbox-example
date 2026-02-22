using System.Data;
using Dapper;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    public async Task<Order> CreateAsync(Order order, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO orders.orders (id, goods_name, price, created_at, status)
            VALUES (@Id, @GoodsName, @Price, @CreatedAt, @Status)
            RETURNING id, goods_name AS GoodsName, price, created_at AS CreatedAt, status
            """;

        return await connection.QuerySingleAsync<Order>(sql, new
        {
            order.Id,
            order.GoodsName,
            order.Price,
            order.CreatedAt,
            Status = order.Status.ToString()
        }, transaction);
    }
}
