using FluentMigrator;

namespace OrderService.Infrastructure.Migrations;

[Migration(20240105)]
public class Migration_20240105_AddRetryColumnsToOutboxMessages : Migration
{
    public override void Up()
    {
        Execute.Sql("""
            ALTER TABLE orders.outbox_messages
                ADD COLUMN retry_count INT NOT NULL DEFAULT 0,
                ADD COLUMN max_retries INT NOT NULL DEFAULT 5,
                ADD COLUMN last_attempted_at TIMESTAMPTZ NULL,
                ADD COLUMN next_retry_at TIMESTAMPTZ NOT NULL DEFAULT now();
            """);
    }

    public override void Down()
    {
        Execute.Sql("""
            ALTER TABLE orders.outbox_messages
                DROP COLUMN retry_count,
                DROP COLUMN max_retries,
                DROP COLUMN last_attempted_at,
                DROP COLUMN next_retry_at;
            """);
    }
}
