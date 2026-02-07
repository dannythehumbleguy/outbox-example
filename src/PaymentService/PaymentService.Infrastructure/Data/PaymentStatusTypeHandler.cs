using System.Data;
using Dapper;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Data;

public class PaymentStatusTypeHandler : SqlMapper.TypeHandler<PaymentStatus>
{
    public override void SetValue(IDbDataParameter parameter, PaymentStatus value)
    {
        parameter.Value = value.ToString();
    }

    public override PaymentStatus Parse(object value)
    {
        return Enum.Parse<PaymentStatus>(value.ToString()!);
    }
}
