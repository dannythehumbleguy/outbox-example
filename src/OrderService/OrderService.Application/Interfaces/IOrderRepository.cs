using OrderService.Domain.Entities;

namespace OrderService.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order> CreateAsync(Order order);
}
