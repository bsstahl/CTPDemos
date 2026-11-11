namespace WorkOrderService.Messages;

/// <summary>The new work order and the outcomes of operations attempted for the request.</summary>
internal sealed record CreateWorkOrderResponse(WorkOrder WorkOrder, IReadOnlyList<OperationResult> Operations);
