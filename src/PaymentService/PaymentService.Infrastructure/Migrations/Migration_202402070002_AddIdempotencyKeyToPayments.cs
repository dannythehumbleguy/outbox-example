using FluentMigrator;

namespace PaymentService.Infrastructure.Migrations;

[Migration(202402070002)]
public class Migration_202402070002_AddIdempotencyKeyToPayments : Migration
{
    public override void Up()
    {
        Alter.Table("payments").InSchema("payment")
            .AddColumn("idempotency_key").AsGuid().NotNullable().WithDefaultValue(Guid.Empty);

        Create.UniqueConstraint("uq_payments_idempotency_key")
            .OnTable("payments").WithSchema("payment")
            .Column("idempotency_key");
    }

    public override void Down()
    {
        Delete.UniqueConstraint("uq_payments_idempotency_key")
            .FromTable("payments").InSchema("payment");

        Delete.Column("idempotency_key").FromTable("payments").InSchema("payment");
    }
}
