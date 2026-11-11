using Microsoft.AspNetCore.Http.HttpResults;
using WorkOrderService.Messages;
using WorkOrderService.Services;

namespace WorkOrderService.Endpoints;

internal static class WorkOrderEndpoints
{
    public static void MapWorkOrders(this WebApplication app)
    {
        app.MapPost("/telemetry", async Task<Results<Created<CreateWorkOrderResponse>, ValidationProblem, ProblemHttpResult>> (
            BusTelemetry telemetry, IWorkOrderService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(telemetry.BusId))
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["busId"] = ["A nonblank busId is required."]
                });

            try
            {
                var result = await service.CreateAsync(telemetry, ct).ConfigureAwait(false);
                return TypedResults.Created($"/work-orders/{result.WorkOrder.WorkOrderId}", result);
            }
            catch (OperationFailedException ex)
            {
                return TypedResults.Problem(statusCode: ex.InnerException is VendorNotificationException ? StatusCodes.Status502BadGateway : StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("ReceiveBusTelemetry")
        .WithSummary("Save a new work order, then notify the vendor.")
        .WithDescription("Every request creates a new work order, even for a repeated alertId. The two writes are not atomic.")
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/work-orders/{workOrderId}", Results<Ok<WorkOrder>, NotFound> (
            string workOrderId, IWorkOrderService service, CancellationToken ct) =>
        {
            var workOrder = service.Find(workOrderId, ct);
            return workOrder is null ? TypedResults.NotFound() : TypedResults.Ok(workOrder);
        })
        .WithName("GetWorkOrder")
        .WithSummary("Read a saved work order from SQLite.");
    }
}
