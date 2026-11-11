using WorkOrderService.Messages;

namespace WorkOrderService.Services;

internal interface IWorkOrderStore
{
    void Initialize();
    Task SaveWorkOrderAsync(WorkOrder workOrder, CancellationToken ct);
    WorkOrder? Find(string workOrderId, CancellationToken ct);

    Task<Transaction> BeginUpdateMessage(WorkOrder workOrder, CancellationToken ct) 
        => throw new NotSupportedException("Transactions are not supported.");
    Task Commit(Transaction trx)
        => throw new NotSupportedException("Transactions are not supported.");
}
