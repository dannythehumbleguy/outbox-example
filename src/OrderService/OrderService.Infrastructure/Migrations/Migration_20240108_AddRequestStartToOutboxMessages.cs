using FluentMigrator;

namespace OrderService.Infrastructure.Migrations;

[Migration(20240108)]
public class Migration_20240108_AddRequestStartToOutboxMessages : Migration
{
    public override void Up() => Execute.Sql("ALTER TABLE orders.outbox_messages ADD COLUMN request_started_at TIMESTAMPTZ NULL;");

    public override void Down() => Execute.Sql("ALTER TABLE orders.outbox_messages DROP COLUMN request_started_at;");
}
