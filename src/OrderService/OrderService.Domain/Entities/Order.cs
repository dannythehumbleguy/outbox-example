using System.Text.Json.Serialization;

namespace OrderService.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string GoodsName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Created,
    Processing,
    Completed,
    Cancelled
}
