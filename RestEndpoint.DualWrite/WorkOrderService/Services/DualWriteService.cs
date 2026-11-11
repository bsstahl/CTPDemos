using WorkOrderService.Messages;
using WorkOrderService.Extensions;

namespace WorkOrderService.Services;

internal sealed class DualWriteService(IWorkOrderStore store, IVendorNotifier vendor) : IWorkOrderService
{
    public async Task<CreateWorkOrderResponse> CreateAsync(BusTelemetry telemetry, CancellationToken ct)
    {
        var workOrder = WorkOrderFactory.Create(telemetry);
        var operationResults = new List<OperationResult>();

        // Reverse these two calls to demonstrate the other dual-write failure modality.
        await store.SaveWorkOrderAsync(workOrder, ct).ConfigureAwait(false);
        await vendor.NotifyVendorAsync(workOrder, ct).ConfigureAwait(false);

        // Use a transaction to **attempt to insure** that both operations succeed or fail together.
        // Note: this actually has the opposite effect for reasons enumerated in the presentation
        // var trx = await store.BeginUpdateMessage(workOrder, ct).ConfigureAwait(false);
        // await vendor.NotifyVendorAsync(workOrder, ct).ConfigureAwait(false);
        // await store.Commit(trx).ConfigureAwait(false);

        return new CreateWorkOrderResponse(workOrder, operationResults);
    }

    public WorkOrder? Find(string workOrderId, CancellationToken ct) => store.Find(workOrderId, ct);
}
