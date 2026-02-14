using KafkaFlow;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.Infrastructure;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

var lokiUrl = builder.Configuration["Loki:Url"]
    ?? throw new InvalidOperationException("Loki URL configuration not found.");

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .WriteTo.Console()
        .WriteTo.GrafanaLoki(lokiUrl, labels:
        [
            new LokiLabel { Key = "app", Value = "order-service" }
        ]);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var kafkaBrokers = builder.Configuration["Kafka:Brokers"]
    ?? throw new InvalidOperationException("Kafka brokers configuration not found.");

var tempoEndpoint = builder.Configuration["Otel:TempoEndpoint"]
    ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("order-service"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("Npgsql")
        .AddSource("OrderService.Kafka")
        .AddOtlpExporter(options => options.Endpoint = new Uri(tempoEndpoint)));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(connectionString, kafkaBrokers);

var app = builder.Build();

DependencyInjection.ApplyMigrations(app.Services);

var kafkaBus = app.Services.CreateKafkaBus();
await kafkaBus.StartAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
