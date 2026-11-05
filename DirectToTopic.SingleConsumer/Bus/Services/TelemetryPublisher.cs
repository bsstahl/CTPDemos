using System.Text.Json;
using Bus.Messages;
using Confluent.Kafka;

namespace Bus.Services;

internal static class TelemetryPublisher
{
    public const string BootstrapServers = "localhost:9092";
    public const string Topic = "bus-telemetry";

    public static async Task<DeliveryResult<string, string>> PublishAsync(
        BusTelemetry telemetry,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var config = new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            BrokerAddressFamily = BrokerAddressFamily.V4,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 10_000,
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();

        // Keying by bus ID keeps each bus's readings in order within a partition.
        var message = new Message<string, string>
        {
            Key = telemetry.BusId,
            Value = JsonSerializer.Serialize(telemetry, BusTelemetryJsonContext.Default.BusTelemetry),
        };

        return await producer.ProduceAsync(Topic, message, ct).ConfigureAwait(false);
    }
}
