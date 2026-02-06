using OrderService.Application.Dto;

namespace OrderService.Application.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
}
