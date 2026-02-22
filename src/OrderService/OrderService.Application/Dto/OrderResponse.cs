using OrderService.Domain.Entities;

namespace OrderService.Application.Dto;

public class OrderResponse
{
    public Guid Id { get; set; }
    public string GoodsName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
}
