using FluentMigrator;

namespace OrderService.Infrastructure.Migrations;

[Migration(20240101)]
public class Migration_20240101_CreateOrdersTable : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS orders");

        Execute.Sql(@"
            CREATE TABLE orders.orders (
                id UUID PRIMARY KEY,
                goods_name VARCHAR(255) NOT NULL,
                price DECIMAL(18, 2) NOT NULL,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL,
                status VARCHAR(50) NOT NULL
            );
        ");
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS orders.orders;");
        Execute.Sql("DROP SCHEMA IF EXISTS orders");
    }
}
