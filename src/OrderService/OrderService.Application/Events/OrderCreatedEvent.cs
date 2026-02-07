namespace OrderService.Application.Events;

public record OrderCreatedEvent(Guid Id, decimal Price);
