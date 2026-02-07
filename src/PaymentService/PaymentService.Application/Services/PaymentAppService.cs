using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Services;

public class PaymentAppService(IPaymentRepository paymentRepository) : IPaymentService
{
    public async Task<Payment> ProcessOrderAsync(Guid orderId, decimal amount)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
            Status = PaymentStatus.Completed
        };

        return await paymentRepository.CreateAsync(payment);
    }
}
