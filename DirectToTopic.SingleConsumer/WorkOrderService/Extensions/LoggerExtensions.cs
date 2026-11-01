namespace WorkOrderService.Extensions;

internal static partial class LoggerExtensions
{
    [LoggerMessage(1, LogLevel.Debug, "Kafka topic {Topic} already exists")]
    internal static partial void KafkaTopicAlreadyExists(this ILogger logger, string topic);

    [LoggerMessage(2, LogLevel.Information, "Listening for bus telemetry on topic {Topic}")]
    internal static partial void ListeningForBusTelemetry(this ILogger logger, string topic);

    [LoggerMessage(3, LogLevel.Warning, "Skipping Kafka tombstone message")]
    internal static partial void SkippingTombstoneMessage(this ILogger logger);

    [LoggerMessage(4, LogLevel.Warning, "Skipping empty telemetry payload")]
    internal static partial void SkippingEmptyTelemetryPayload(this ILogger logger);

    [LoggerMessage(5, LogLevel.Information, "Skipping duplicate alert {AlertId}")]
    internal static partial void SkippingDuplicateAlert(this ILogger logger, Guid alertId);

    [LoggerMessage(6, LogLevel.Information, "Created work order {Path}")]
    internal static partial void CreatedWorkOrder(this ILogger logger, string path);

    [LoggerMessage(7, LogLevel.Warning, "Skipping unprocessable message")]
    internal static partial void SkippingUnprocessableMessage(this ILogger logger, Exception exception);
}
