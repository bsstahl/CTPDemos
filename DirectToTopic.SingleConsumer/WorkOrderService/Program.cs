using Microsoft.Extensions.Hosting;
using WorkOrderService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddKafkaConsumer<string, string>("kafka", settings =>
{
    settings.Config.GroupId = "work-order-service";
    settings.Config.AutoOffsetReset = Confluent.Kafka.AutoOffsetReset.Earliest;
    settings.Config.AllowAutoCreateTopics = true;
});

builder.Services.AddSingleton(sp =>
    new WorkOrderWriter(builder.Configuration["WorkOrders:Directory"] ?? "work-orders"));
builder.Services.AddHostedService<TelemetryConsumer>();

builder.Build().Run();
