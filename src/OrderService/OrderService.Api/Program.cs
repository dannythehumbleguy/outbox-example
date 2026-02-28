using KafkaFlow;
using OpenTelemetry.Resources;
using Prometheus;
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
        .Enrich.FromLogContext()
        .Enrich.With(new ActivityEnricher())
        .Enrich.WithProperty("Application", "order-service")
        .Enrich.WithProperty("Instance", Environment.MachineName)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {TraceId} {SpanId}{NewLine}{Exception}")
        .WriteTo.GrafanaLoki(lokiUrl, labels:
        [
            new LokiLabel { Key = "app", Value = "order-service" },
            new LokiLabel { Key = "instance", Value = Environment.MachineName }
        ], textFormatter: new LokiFormatter());
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var kafkaBrokers = builder.Configuration["Kafka:Brokers"]
    ?? throw new InvalidOperationException("Kafka brokers configuration not found.");

var tempoEndpoint = builder.Configuration["Otel:TempoEndpoint"]
    ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("order-service", serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource("OrderService.Kafka")
        .AddSource("OrderService.Outbox")
        .AddOtlpExporter(options => options.Endpoint = new Uri(tempoEndpoint)));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration, connectionString, kafkaBrokers);

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"] ?? "";
if (!string.IsNullOrEmpty(pathBase))
    app.UsePathBase(pathBase);

DependencyInjection.ApplyMigrations(app.Services);

var kafkaBus = app.Services.CreateKafkaBus();
await kafkaBus.StartAsync();

app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapMetrics();

app.Run();
