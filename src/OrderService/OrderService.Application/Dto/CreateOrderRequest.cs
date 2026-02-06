namespace OrderService.Application.Dto;

public class CreateOrderRequest
{
    public string GoodsName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
