using WorkOrderService.Messages;

namespace WorkOrderService.Services;

internal interface IWorkOrderService
{
    Task<CreateWorkOrderResponse> CreateAsync(BusTelemetry telemetry, CancellationToken ct);
    WorkOrder? Find(string workOrderId, CancellationToken ct);
}
