using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddKafkaConsumer<string, string>("kafka", settings =>
{
    settings.Config.GroupId = "work-order-service";
    settings.Config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
    settings.Config.AllowAutoCreateTopics = true;
    settings.Config.EnableAutoOffsetStore = false;
});

builder.Services.AddSingleton(sp =>
    new WorkOrderService.Services.WorkOrderWriter(builder.Configuration["WorkOrders:Directory"] ?? "work-orders"));
builder.Services.AddHostedService<WorkOrderService.TelemetryConsumer>();

builder.Build().Run();
