using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Messaging;

public class OutboxHeartbeatWorker(
    IDbConnectionFactory connectionFactory,
    IOptions<OutboxHeartbeatOptions> options,
    ILogger<OutboxHeartbeatWorker> logger) : BackgroundService
{
    private readonly TimeSpan _processingTimeout = TimeSpan.FromSeconds(options.Value.ProcessingTimeoutSeconds);
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ResetStalledMessagesAsync();
            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ResetStalledMessagesAsync()
    {
        var threshold = DateTimeOffset.UtcNow - _processingTimeout;

        using var connection = connectionFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(
            $"""
             UPDATE orders.outbox_messages
             SET status = '{nameof(OutboxMessageStatus.Created)}', started_processing_at = NULL
             WHERE status = '{nameof(OutboxMessageStatus.Processing)}'
               AND started_processing_at < @Threshold
             """,
            new { Threshold = threshold });

        if (affected > 0)
            logger.LogWarning("Reset {Count} stalled outbox message(s) from Processing back to Created", affected);
    }
}
