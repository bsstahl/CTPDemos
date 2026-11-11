using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WorkOrderService.Extensions;
using WorkOrderService.Services;

namespace WorkOrderService.Middleware;

internal sealed class OperationExceptionHandler(ILogger<OperationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not OperationFailedException failure) return false;

        logger.OperationFailed(failure);
        var status = failure.InnerException is VendorNotificationException
            ? StatusCodes.Status502BadGateway
            : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = failure.InnerException is DataStoreUpdateException
                ? "Data store update failed"
                : "Telemetry processing failed",
            Detail = "The system is in an unknown, possibly inconsistent state. Verify the database and vendor system before retrying."
        }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }
}
