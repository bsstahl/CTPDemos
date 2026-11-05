using Microsoft.Extensions.Hosting;
using WorkOrderService.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddKafkaConsumer<string, string>("kafka", settings =>
{
    settings.Config.BrokerAddressFamily = Confluent.Kafka.BrokerAddressFamily.V4;
    settings.Config.GroupId = "work-order-service";
    settings.Config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
    settings.Config.AllowAutoCreateTopics = true;
    settings.Config.EnableAutoOffsetStore = false;
});

builder.Services.AddSingleton(sp => new WorkOrderWriter(
    builder.Configuration["WorkOrders:Directory"] ?? "work-orders",
    sp.GetRequiredService<ILogger<WorkOrderWriter>>()));
    
builder.Services.AddHostedService<TelemetryConsumer>();

builder.Build().Run();
