using FluentMigrator;

namespace PaymentService.Infrastructure.Migrations;

[Migration(202402070001)]
public class Migration_202402070001_CreatePaymentsTable : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE SCHEMA IF NOT EXISTS payment");

        Create.Table("payments").InSchema("payment")
            .WithColumn("id").AsGuid().PrimaryKey()
            .WithColumn("order_id").AsGuid().NotNullable()
            .WithColumn("amount").AsDecimal(18, 2).NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("status").AsString(50).NotNullable();

        Create.Index("ix_payments_order_id")
            .OnTable("payments").InSchema("payment")
            .OnColumn("order_id");
    }

    public override void Down()
    {
        Delete.Table("payments").InSchema("payment");
        Execute.Sql("DROP SCHEMA IF EXISTS payment");
    }
}
