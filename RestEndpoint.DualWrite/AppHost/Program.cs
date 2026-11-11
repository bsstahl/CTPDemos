var builder = DistributedApplication.CreateBuilder(args);

var databasePath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "data", "work-orders.db"));

var service = builder.AddProject<Projects.WorkOrderService>("work-order-service")
    .WithEnvironment("WorkOrders__DatabasePath", databasePath)
    .WithHttpHealthCheck("/health");

if (builder.Configuration["Vendor:NotificationUrl"] is { } vendorUrl)
    service.WithEnvironment("Vendor__NotificationUrl", vendorUrl);

builder.Build().Run();
