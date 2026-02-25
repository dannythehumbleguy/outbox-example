using Confluent.Kafka;
using Dapper;
using FluentMigrator.Runner;
using FluentMigrator.Runner.VersionTableInfo;
using KafkaFlow;
using KafkaFlow.Serializer;
using Microsoft.Extensions.Configuration;
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
        IConfiguration configuration,
        string connectionString,
        string kafkaBrokers)
    {
        services.Configure<OutboxHeartbeatOptions>(configuration.GetSection(OutboxHeartbeatOptions.SectionName));
        SqlMapper.AddTypeHandler(new OrderStatusTypeHandler());
        SqlMapper.AddTypeHandler(new OutboxMessageStatusTypeHandler());

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<IOrderService, OrderAppService>();
        services.AddScoped<IEventPublisher, KafkaEventPublisher>();
        services.AddScoped<IOutboxMessageHandler, OrderCreatedOutboxHandler>();
        services.AddHostedService<OutboxPublisherWorker>();
        services.AddHostedService<OutboxHeartbeatWorker>();

        services.AddSingleton<IVersionTableMetaData, VersionTableMetaData>();

        services.AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(DependencyInjection).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        var producerOptions = configuration
            .GetSection(KafkaProducerOptions.SectionName)
            .Get<KafkaProducerOptions>() ?? new KafkaProducerOptions();

        services.AddKafka(kafka => kafka
            .AddCluster(cluster => cluster
                .WithBrokers(kafkaBrokers.Split(','))
                .CreateTopicIfNotExists(KafkaTopics.OrderEvents, 3, 1)
                .AddProducer(KafkaProducers.OrderEvents, producer => producer
                    .DefaultTopic(KafkaTopics.OrderEvents)
                    .WithProducerConfig(new ProducerConfig
                    {
                        MessageSendMaxRetries = producerOptions.MessageSendMaxRetries,
                        MessageTimeoutMs = producerOptions.MessageTimeoutMs,
                        RequestTimeoutMs = producerOptions.RequestTimeoutMs
                    })
                    .AddMiddlewares(middlewares => middlewares
                        .Add<TracingProducerMiddleware>()
                        .AddSerializer<JsonCoreSerializer, CustomMessageTypeResolver>()))));

        return services;
    }

    public static void ApplyMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
