using OrderService.Application.Dto;
using OrderService.Application.Events;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services;

public class OrderAppService(
    IUnitOfWork unitOfWork,
    IOrderRepository orderRepository,
    IOutboxMessageRepository outboxRepository) : IOrderService
{
    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            GoodsName = request.GoodsName,
            Price = request.Price,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = OrderStatus.Created
        };

        var orderCreatedEvent = new OrderCreatedEvent(order.Id, order.Price);
        var outboxMessage = OutboxMessage.From(orderCreatedEvent);

        var createdOrder = await unitOfWork.ExecuteAsync(async (conn, tx) =>
        {
            var result = await orderRepository.CreateAsync(order, conn, tx);
            await outboxRepository.CreateAsync(outboxMessage, conn, tx);
            return result;
        });

        return new OrderResponse
        {
            Id = createdOrder.Id,
            GoodsName = createdOrder.GoodsName,
            Price = createdOrder.Price,
            CreatedAt = createdOrder.CreatedAt,
            Status = createdOrder.Status
        };
    }
}
