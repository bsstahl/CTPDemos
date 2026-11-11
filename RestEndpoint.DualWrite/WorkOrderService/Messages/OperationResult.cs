namespace WorkOrderService.Messages;

/// <summary>The observed outcome of an operation that was attempted.</summary>
internal sealed record OperationResult(string Operation, bool Succeeded);
