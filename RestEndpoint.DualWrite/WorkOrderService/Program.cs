using WorkOrderService.Endpoints;
using WorkOrderService.Middleware;
using WorkOrderService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<OperationExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IWorkOrderStore, SqliteWorkOrderStore>();
builder.Services.AddScoped<IWorkOrderService, DualWriteService>();
builder.Services.AddHttpClient<IVendorNotifier, VendorNotifier>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Vendor:NotificationUrl"] ?? "https://httpbin.org/post");
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    CheckCertificateRevocationList = true
});

var app = builder.Build();

app.Services.GetRequiredService<IWorkOrderStore>().Initialize();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapWorkOrders();
app.MapGet("/health", () => TypedResults.Ok()).ExcludeFromDescription();

app.Run();

internal sealed partial class Program;
