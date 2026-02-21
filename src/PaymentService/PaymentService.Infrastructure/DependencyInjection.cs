using Dapper;
using FluentMigrator.Runner;
using FluentMigrator.Runner.VersionTableInfo;
using KafkaFlow;
using KafkaFlow.Serializer;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Constants;
using PaymentService.Application.Events;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Services;
using PaymentService.Infrastructure.Data;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Repositories;

namespace PaymentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string kafkaBrokers)
    {
        SqlMapper.AddTypeHandler(new PaymentStatusTypeHandler());

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentService, PaymentAppService>();

        services.AddSingleton<IVersionTableMetaData, VersionTableMetaData>();

        services.AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(DependencyInjection).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        services.AddKafka(kafka => kafka
            .AddCluster(cluster => cluster
                .WithBrokers(kafkaBrokers.Split(','))
                .AddConsumer(consumer => consumer
                    .Topic(KafkaTopics.OrderEvents)
                    .WithGroupId(KafkaConsumerGroups.PaymentService)
                    .WithBufferSize(100)
                    .WithWorkersCount(3)
                    .AddMiddlewares(middlewares => middlewares
                        .Add<TracingConsumerMiddleware>()
                        .AddDeserializer<JsonCoreDeserializer, CustomMessageTypeResolver>()
                        .AddTypedHandlers(handlers => handlers
                            .AddHandler<OrderCreatedHandler>())))));

        return services;
    }

    public static void ApplyMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE SCHEMA IF NOT EXISTS payment";
        command.ExecuteNonQuery();

        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
