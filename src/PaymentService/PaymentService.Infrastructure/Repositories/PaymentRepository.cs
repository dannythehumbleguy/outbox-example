using Dapper;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Data;

namespace PaymentService.Infrastructure.Repositories;

public class PaymentRepository(IDbConnectionFactory connectionFactory) : IPaymentRepository
{
    public async Task<Payment> CreateAsync(Payment payment)
    {
        const string sql = """
            INSERT INTO payment.payments (id, order_id, amount, created_at, status)
            VALUES (@Id, @OrderId, @Amount, @CreatedAt, @Status)
            RETURNING id, order_id AS OrderId, amount, created_at AS CreatedAt, status
            """;

        using var connection = connectionFactory.CreateConnection();
        var createdPayment = await connection.QuerySingleAsync<Payment>(sql, new
        {
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.CreatedAt,
            Status = payment.Status.ToString()
        });
        return createdPayment;
    }

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId)
    {
        const string sql = """
            SELECT id, order_id AS OrderId, amount, created_at AS CreatedAt, status
            FROM payment.payments
            WHERE order_id = @OrderId
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Payment>(sql, new { OrderId = orderId });
    }
}
