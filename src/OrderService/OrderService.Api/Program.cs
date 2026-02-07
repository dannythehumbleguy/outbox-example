using KafkaFlow;
using OrderService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var kafkaBrokers = builder.Configuration["Kafka:Brokers"]
    ?? throw new InvalidOperationException("Kafka brokers configuration not found.");

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
