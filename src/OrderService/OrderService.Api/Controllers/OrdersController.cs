using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Dto;
using OrderService.Application.Interfaces;
using Prometheus;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    private static readonly Counter Requests = Metrics.CreateCounter(
        "order_create_requests_total", "Number of order creation requests received by the controller");

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        Requests.Inc();
        var order = await orderService.CreateOrderAsync(request);
        return Created($"/api/orders/{order.Id}", order);
    }
}
