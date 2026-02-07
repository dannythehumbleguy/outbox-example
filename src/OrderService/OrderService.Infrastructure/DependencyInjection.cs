using Dapper;
using FluentMigrator.Runner;
using KafkaFlow;
using KafkaFlow.Serializer;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Constants;
using OrderService.Application.Interfaces;
using OrderService.Application.Services;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Repositories;

namespace OrderService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string kafkaBrokers)
    {
        SqlMapper.AddTypeHandler(new OrderStatusTypeHandler());

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderAppService>();
        services.AddScoped<IEventPublisher, KafkaEventPublisher>();

        services.AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(DependencyInjection).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        services.AddKafka(kafka => kafka
            .AddCluster(cluster => cluster
                .WithBrokers(kafkaBrokers.Split(','))
                .CreateTopicIfNotExists(KafkaTopics.OrderEvents, 1, 1)
                .AddProducer(KafkaProducers.OrderEvents, producer => producer
                    .DefaultTopic(KafkaTopics.OrderEvents)
                    .AddMiddlewares(middlewares => middlewares
                        .AddSerializer<JsonCoreSerializer>()))));

        return services;
    }

    public static void ApplyMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
