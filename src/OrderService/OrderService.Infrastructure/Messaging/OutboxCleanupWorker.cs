using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Messaging;

public class OutboxCleanupWorker(
    IDbConnectionFactory connectionFactory,
    IOptions<OutboxCleanupOptions> options,
    ILogger<OutboxCleanupWorker> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DeleteProcessedMessagesAsync();
            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task DeleteProcessedMessagesAsync()
    {
        var threshold = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(options.Value.RetentionMinutes);

        using var connection = connectionFactory.CreateConnection();

        var deleted = await connection.ExecuteAsync(
            $"""
             DELETE FROM orders.outbox_messages
             WHERE id IN (
                 SELECT id FROM orders.outbox_messages
                 WHERE status = '{nameof(OutboxMessageStatus.Processed)}'
                   AND processed_at < @Threshold
                 LIMIT @BatchSize
             )
             """,
            new { Threshold = threshold, options.Value.BatchSize });

        if (deleted > 0)
            logger.LogInformation("Deleted {Count} processed outbox message(s) older than {RetentionMinutes} minutes", deleted, options.Value.RetentionMinutes);
    }
}
