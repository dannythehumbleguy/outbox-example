using System.Data;
using Dapper;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Data;

public class OrderStatusTypeHandler : SqlMapper.TypeHandler<OrderStatus>
{
    public override void SetValue(IDbDataParameter parameter, OrderStatus value)
    {
        parameter.Value = value.ToString();
    }

    public override OrderStatus Parse(object value)
    {
        return Enum.Parse<OrderStatus>(value.ToString()!);
    }
}
