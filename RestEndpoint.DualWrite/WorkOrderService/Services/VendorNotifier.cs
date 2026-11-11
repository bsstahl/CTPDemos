using WorkOrderService.Extensions;
using WorkOrderService.Messages;

namespace WorkOrderService.Services;

internal sealed class VendorNotifier(HttpClient client, ILogger<VendorNotifier> logger) : IVendorNotifier
{
    public async Task NotifyVendorAsync(WorkOrder workOrder, CancellationToken ct)
    {
        var notification = new WorkOrderNotification(
            workOrder.WorkOrderId,
            workOrder.SourceTelemetry.AlertId,
            workOrder.BusId,
            workOrder.CreatedAt,
            workOrder.Priority,
            workOrder.Tasks);

        try
        {
            var endpoint = workOrder.SourceTelemetry.VendorError
                ? new Uri("https://httpbin.org/status/503")
                : client.BaseAddress;
            using var response = await client.PostAsJsonAsync(
                endpoint, notification, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            logger.VendorNotified(workOrder.WorkOrderId);
        }
        catch (Exception ex)
        {
            throw new VendorNotificationException(workOrder.WorkOrderId, ex);
        }
    }
}
