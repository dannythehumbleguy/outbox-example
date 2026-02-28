using FluentMigrator;

namespace OrderService.Infrastructure.Migrations;

[Migration(20240107)]
public class Migration_20240107_AddOccurredOnIndexToOutboxMessages : Migration
{
    public override void Up()
    {
        Execute.Sql("""
            CREATE INDEX ix_outbox_messages_occurred_on
                ON orders.outbox_messages (occurred_on);
            """);
    }

    public override void Down()
    {
        Execute.Sql("""
            DROP INDEX IF EXISTS orders.ix_outbox_messages_occurred_on;
            """);
    }
}
