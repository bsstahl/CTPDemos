using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace WorkOrderService;

public class TelemetryConsumer(
    IConsumer<string, string> consumer,
    WorkOrderWriter writer,
    IConfiguration config,
    ILogger<TelemetryConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken ct)
    {
        var topic = config["Telemetry:Topic"] ?? "bus-telemetry";
        using (var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = config.GetConnectionString("kafka")
                ?? throw new InvalidOperationException("Kafka connection string is unavailable")
        }).Build())
        {
            try
            {
                await adminClient.CreateTopicsAsync([
                    new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }
                ]);
            }
            catch (CreateTopicsException ex) when (
                ex.Results.Count > 0
                && ex.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                logger.LogDebug("Kafka topic {Topic} already exists", topic);
            }
        }

        consumer.Subscribe(topic);
        logger.LogInformation("Listening for bus telemetry on topic {Topic}", topic);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(ct);
                if (result?.Message is null) continue;

                var telemetry = JsonSerializer.Deserialize<BusTelemetry>(result.Message.Value);
                if (telemetry is null) continue;

                var workOrder = WorkOrderFactory.Create(telemetry);
                var path = await writer.WriteAsync(workOrder, ct);
                if (path is null)
                    logger.LogInformation("Skipping duplicate work order {WorkOrderId}", workOrder.WorkOrderId);
                else
                    logger.LogInformation("Created work order {Path}", path);
                consumer.StoreOffset(result);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (ex is JsonException or ConsumeException)
            {
                logger.LogWarning(ex, "Skipping unprocessable message");
            }
        }
        consumer.Close();
    }
}
