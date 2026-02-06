using Dapper;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repositories;

public class OrderRepository(IDbConnectionFactory connectionFactory) : IOrderRepository
{
    public async Task<Order> CreateAsync(Order order)
    {
        const string sql = """
            INSERT INTO orders (id, goods_name, price, created_at, status)
            VALUES (@Id, @GoodsName, @Price, @CreatedAt, @Status)
            RETURNING id, goods_name AS GoodsName, price, created_at AS CreatedAt, status
            """;

        using var connection = connectionFactory.CreateConnection();
        var createdOrder = await connection.QuerySingleAsync<Order>(sql, new
        {
            order.Id,
            order.GoodsName,
            order.Price,
            order.CreatedAt,
            Status = order.Status.ToString()
        });
        return createdOrder;
    }
}
