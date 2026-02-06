using Dapper;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Interfaces;
using OrderService.Application.Services;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Repositories;

namespace OrderService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        SqlMapper.AddTypeHandler(new OrderStatusTypeHandler());

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderAppService>();

        services.AddFluentMigratorCore()
            .ConfigureRunner(runner => runner
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(DependencyInjection).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }

    public static void ApplyMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }
}
