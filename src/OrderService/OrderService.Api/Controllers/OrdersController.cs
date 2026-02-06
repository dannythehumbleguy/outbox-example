using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Dto;
using OrderService.Application.Interfaces;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = await orderService.CreateOrderAsync(request);
        return Created($"/api/orders/{order.Id}", order);
    }
}
