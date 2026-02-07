using OrderService.Application.Constants;
using OrderService.Application.Dto;
using OrderService.Application.Events;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services;

public class OrderAppService(IOrderRepository orderRepository, IEventPublisher eventPublisher) : IOrderService
{
    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            GoodsName = request.GoodsName,
            Price = request.Price,
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Created
        };

        var createdOrder = await orderRepository.CreateAsync(order);

        var orderCreatedEvent = new OrderCreatedEvent(createdOrder.Id, createdOrder.Price);
        await eventPublisher.PublishAsync(KafkaTopics.OrderEvents, orderCreatedEvent);

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
