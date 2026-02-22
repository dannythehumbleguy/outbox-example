using Dapper;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Data;

namespace PaymentService.Infrastructure.Repositories;

public class PaymentRepository(IDbConnectionFactory connectionFactory) : IPaymentRepository
{
    public async Task<Payment> CreateAsync(Payment payment)
    {
        const string insertSql = """
            INSERT INTO payment.payments (id, order_id, amount, created_at, status, idempotency_key)
            VALUES (@Id, @OrderId, @Amount, @CreatedAt, @Status, @IdempotencyKey)
            ON CONFLICT (idempotency_key) DO NOTHING
            RETURNING id, order_id AS OrderId, amount, created_at AS CreatedAt, status, idempotency_key AS IdempotencyKey
            """;

        const string fetchSql = """
            SELECT id, order_id AS OrderId, amount, created_at AS CreatedAt, status, idempotency_key AS IdempotencyKey
            FROM payment.payments
            WHERE idempotency_key = @IdempotencyKey
            """;

        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QuerySingleOrDefaultAsync<Payment>(insertSql, new
        {
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.CreatedAt,
            Status = payment.Status.ToString(),
            payment.IdempotencyKey
        });

        // ON CONFLICT DO NOTHING returns no rows — fetch the existing payment
        return result ?? await connection.QuerySingleAsync<Payment>(fetchSql, new { payment.IdempotencyKey });
    }

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId)
    {
        const string sql = """
            SELECT id, order_id AS OrderId, amount, created_at AS CreatedAt, status, idempotency_key AS IdempotencyKey
            FROM payment.payments
            WHERE order_id = @OrderId
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Payment>(sql, new { OrderId = orderId });
    }
}
