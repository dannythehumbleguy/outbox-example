namespace OrderService.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string GoodsName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
}
