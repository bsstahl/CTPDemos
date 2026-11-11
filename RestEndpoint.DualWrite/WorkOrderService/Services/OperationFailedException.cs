using System.Diagnostics.CodeAnalysis;
using WorkOrderService.Messages;

namespace WorkOrderService.Services;

[SuppressMessage("Design", "CA1032:Implement standard exception constructors",
    Justification = "Operation failures requires the observed operation outcomes.")]
internal sealed class OperationFailedException(IReadOnlyList<OperationResult> operations, Exception innerException)
    : Exception("An operation failed while processing telemetry.", innerException)
{
    public IReadOnlyList<OperationResult> Operations { get; } = operations;
}
