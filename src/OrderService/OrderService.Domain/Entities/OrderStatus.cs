using System.Text.Json.Serialization;

namespace OrderService.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Created,
    Processing,
    Completed,
    Cancelled
}
