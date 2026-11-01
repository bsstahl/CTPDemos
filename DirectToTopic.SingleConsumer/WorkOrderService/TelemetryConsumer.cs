using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using WorkOrderService.Extensions;
using WorkOrderService.Messages;
using WorkOrderService.Services;

namespace WorkOrderService;

internal sealed class TelemetryConsumer(
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
                ]).ConfigureAwait(false);
            }
            catch (CreateTopicsException ex) when (
                ex.Results.Count > 0
                && ex.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                logger.KafkaTopicAlreadyExists(topic);
            }
        }

        consumer.Subscribe(topic);
        logger.ListeningForBusTelemetry(topic);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(ct);
                if (result?.Message is null) continue;

                if (result.Message.Value is null)
                {
                    logger.SkippingTombstoneMessage();
                    consumer.StoreOffset(result);
                    continue;
                }

                var telemetry = JsonSerializer.Deserialize<BusTelemetry>(result.Message.Value);
                if (telemetry is null)
                {
                    logger.SkippingEmptyTelemetryPayload();
                    consumer.StoreOffset(result);
                    continue;
                }

                var workOrder = WorkOrderFactory.Create(telemetry);
                var path = await writer.WriteAsync(workOrder, ct).ConfigureAwait(false);
                if (path is null)
                    logger.SkippingDuplicateAlert(telemetry.AlertId);
                else
                    logger.CreatedWorkOrder(path);
                consumer.StoreOffset(result);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (ex is JsonException or ConsumeException)
            {
                logger.SkippingUnprocessableMessage(ex);
            }
        }
        consumer.Close();
    }
}
