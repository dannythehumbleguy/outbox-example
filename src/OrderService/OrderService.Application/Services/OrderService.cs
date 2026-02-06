using OrderService.Application.Dto;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services;

public class OrderAppService(IOrderRepository orderRepository) : IOrderService
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
