namespace WorkOrderService.Extensions;

internal static partial class LoggerExtensions
{
    [LoggerMessage(1, LogLevel.Information, "Saved work order {WorkOrderId} for alert {AlertId} to SQLite")]
    internal static partial void WorkOrderSaved(this ILogger logger, string workOrderId, Guid alertId);

    [LoggerMessage(2, LogLevel.Information, "Vendor accepted notification for work order {WorkOrderId}")]
    internal static partial void VendorNotified(this ILogger logger, string workOrderId);

    [LoggerMessage(3, LogLevel.Error, "An operation failed! The system is in an unknown, possibly inconsistent state")]
    internal static partial void OperationFailed(this ILogger logger, Exception exception);
}
