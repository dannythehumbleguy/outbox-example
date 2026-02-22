using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

public interface IPaymentService
{
    Task<Payment> ProcessOrderAsync(Guid orderId, decimal amount, Guid idempotencyKey);
}
