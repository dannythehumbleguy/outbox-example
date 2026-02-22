using System.Data;
using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces;

public interface IOutboxMessageRepository
{
    Task CreateAsync(OutboxMessage outboxMessage, IDbConnection connection, IDbTransaction transaction);
}
