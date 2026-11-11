using WorkOrderService.Messages;

namespace WorkOrderService.Services;

internal interface IVendorNotifier
{
    Task NotifyVendorAsync(WorkOrder workOrder, CancellationToken ct);
}
