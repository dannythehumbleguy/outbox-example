using FluentMigrator;

namespace OrderService.Infrastructure.Migrations;

[Migration(20240104)]
public class Migration_20240104_AddStartedProcessingAtToOutboxMessages : Migration
{
    public override void Up()
    {
        Execute.Sql("ALTER TABLE orders.outbox_messages ADD COLUMN started_processing_at TIMESTAMPTZ NULL;");
    }

    public override void Down()
    {
        Execute.Sql("ALTER TABLE orders.outbox_messages DROP COLUMN started_processing_at;");
    }
}
