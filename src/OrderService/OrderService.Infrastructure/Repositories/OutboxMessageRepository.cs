using System.Data;
using Dapper;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Repositories;

public class OutboxMessageRepository : IOutboxMessageRepository
{
    public async Task CreateAsync(OutboxMessage outboxMessage, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO orders.outbox_messages (id, occurred_on, type, payload, status)
            VALUES (@Id, @OccurredOn, @Type, @Payload::jsonb, @Status)
            """;

        await connection.ExecuteAsync(sql, new
        {
            outboxMessage.Id,
            outboxMessage.OccurredOn,
            outboxMessage.Type,
            outboxMessage.Payload,
            Status = outboxMessage.Status.ToString()
        }, transaction);
    }
}
