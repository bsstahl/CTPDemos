using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Work orders are written here as YAML files so they are easy to open during a demo.
var workOrderDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "work-orders"));

var kafka = builder.AddKafka("kafka", port: 9092)
    .WithContainerName("ctpdemos-kafka")
    .WithEndpoint("internal", endpoint => endpoint.Port = 9093)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEnvironment("KAFKA_LOG_RETENTION_HOURS", "6") // Keep demo topic records for six hours; production should retain them longer to maintain idempotence.
    .WithKafkaUI(); // Browse the topic to inspect telemetry received through the REST proxy

builder.AddContainer("kafka-rest", "confluentinc/cp-kafka-rest", "8.1.1")
    .WithHttpEndpoint(targetPort: 8082)
    .WithEnvironment("KAFKA_REST_HOST_NAME", "kafka-rest")
    .WithEnvironment("KAFKA_REST_LISTENERS", "http://0.0.0.0:8082")
    .WithEnvironment("KAFKA_REST_BOOTSTRAP_SERVERS",
        ReferenceExpression.Create($"{kafka.Resource.Name}:{kafka.Resource.InternalEndpoint.Property(EndpointProperty.TargetPort)}"))
    .WithHttpHealthCheck("/topics")
    .WaitFor(kafka);

builder.AddProject<Projects.WorkOrderService>("work-order-service")
    .WithReference(kafka)
    .WaitFor(kafka)
    .WithEnvironment("WorkOrders__Directory", workOrderDir)
    .WithEnvironment("Telemetry__Topic", "bus-telemetry");

builder.Build().Run();
