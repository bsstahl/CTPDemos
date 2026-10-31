var builder = DistributedApplication.CreateBuilder(args);

// Work orders are written here as YAML files so they are easy to open during a demo.
var workOrderDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "work-orders"));

var kafka = builder.AddKafka("kafka")
    .WithKafkaUI(); // Browse the topic and publish sample telemetry from the browser

builder.AddProject<Projects.WorkOrderService>("work-order-service")
    .WithReference(kafka)
    .WaitFor(kafka)
    .WithEnvironment("WorkOrders__Directory", workOrderDir)
    .WithEnvironment("Telemetry__Topic", "bus-telemetry");

builder.Build().Run();
