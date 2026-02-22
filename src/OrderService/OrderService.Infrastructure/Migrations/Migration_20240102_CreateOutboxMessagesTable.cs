using FluentMigrator;

namespace OrderService.Infrastructure.Migrations;

[Migration(20240102)]
public class Migration_20240102_CreateOutboxMessagesTable : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            CREATE TABLE orders.outbox_messages (
                id UUID PRIMARY KEY,
                occurred_on TIMESTAMPTZ NOT NULL,
                type VARCHAR(255) NOT NULL,
                payload JSONB NOT NULL,
                status VARCHAR(50) NOT NULL DEFAULT 'Created'
            );
        ");
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS orders.outbox_messages;");
    }
}
