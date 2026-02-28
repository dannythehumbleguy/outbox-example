using FluentMigrator;

namespace OrderService.Infrastructure.Migrations;

[Migration(20240106)]
public class Migration_20240106_AddTraceContextToOutboxMessages : Migration
{
    public override void Up()
    {
        Execute.Sql("""
            ALTER TABLE orders.outbox_messages
                ADD COLUMN traceparent VARCHAR(55) NULL;
            """);
    }

    public override void Down()
    {
        Execute.Sql("""
            ALTER TABLE orders.outbox_messages
                DROP COLUMN traceparent;
            """);
    }
}
