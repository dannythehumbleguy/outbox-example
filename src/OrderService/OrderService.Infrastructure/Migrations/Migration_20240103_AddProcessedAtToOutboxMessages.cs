using FluentMigrator;

namespace OrderService.Infrastructure.Migrations;

[Migration(20240103)]
public class Migration_20240103_AddProcessedAtToOutboxMessages : Migration
{
    public override void Up()
    {
        Execute.Sql("ALTER TABLE orders.outbox_messages ADD COLUMN processed_at TIMESTAMPTZ NULL;");
    }

    public override void Down()
    {
        Execute.Sql("ALTER TABLE orders.outbox_messages DROP COLUMN processed_at;");
    }
}
