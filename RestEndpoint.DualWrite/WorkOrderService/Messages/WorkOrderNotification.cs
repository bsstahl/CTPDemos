namespace WorkOrderService.Messages;

/// <summary>A vendor-facing summary of a new work order, without the original telemetry readings.</summary>
internal sealed record WorkOrderNotification(
    string WorkOrderId,
    Guid AlertId,
    string BusId,
    DateTimeOffset CreatedAt,
    string Priority,
    IReadOnlyList<string> Tasks);
