using System.Diagnostics.CodeAnalysis;

namespace WorkOrderService.Services;

[SuppressMessage("Design", "CA1032:Implement standard exception constructors",
    Justification = "A vendor failure must identify the work order whose notification was attempted.")]
internal sealed class VendorNotificationException(string workOrderId, Exception innerException)
    : Exception("The vendor notification did not complete successfully.", innerException)
{
    public string WorkOrderId { get; } = workOrderId;
}
