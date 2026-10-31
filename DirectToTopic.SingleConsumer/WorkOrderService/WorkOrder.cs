namespace WorkOrderService;

public record WorkOrder(
    string WorkOrderId,
    string BusId,
    DateTimeOffset CreatedAt,
    string Priority,
    List<string> Tasks,
    BusTelemetry SourceTelemetry);
