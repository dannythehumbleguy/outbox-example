using System.Data;
using Dapper;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Data;

public class OutboxMessageStatusTypeHandler : SqlMapper.TypeHandler<OutboxMessageStatus>
{
    public override void SetValue(IDbDataParameter parameter, OutboxMessageStatus value)
    {
        parameter.Value = value.ToString();
    }

    public override OutboxMessageStatus Parse(object value)
    {
        return Enum.Parse<OutboxMessageStatus>(value.ToString()!);
    }
}
