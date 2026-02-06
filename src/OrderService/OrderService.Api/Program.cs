using OrderService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

DependencyInjection.ApplyMigrations(app.Services);

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
