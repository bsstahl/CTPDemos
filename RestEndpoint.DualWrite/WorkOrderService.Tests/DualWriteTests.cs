using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkOrderService.Messages;
using WorkOrderService.Services;
using Xunit;

namespace WorkOrderService.Tests;

public sealed class DualWriteTests
{
    private static readonly BusTelemetry Reading = new(
        Guid.Parse("84d7fdbf-4ea0-4a75-bad6-f400fc7881fa"),
        "B-42", DateTimeOffset.Parse("2026-01-15T08:30:00Z", System.Globalization.CultureInfo.InvariantCulture),
        182340.5, 115, 3.2, ["P0217"]);

    [Fact]
    public async Task TransactionWriteIsInvisibleUntilCommit()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IWorkOrderStore>();
        var workOrder = WorkOrderFactory.Create(Reading);
        using var transaction = await store.BeginUpdateMessage(workOrder, TestContextToken);
        Assert.Equal(0, factory.CountRows());
        Assert.Null(store.Find(workOrder.WorkOrderId, TestContextToken));

        await store.Commit(transaction);
        Assert.Equal(1, factory.CountRows());
        Assert.NotNull(store.Find(workOrder.WorkOrderId, TestContextToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.Commit(transaction));
    }

    [Fact]
    public async Task DisposingUncommittedTransactionRollsBackAndReleasesWriter()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IWorkOrderStore>();
        var workOrder = WorkOrderFactory.Create(Reading);
        using (await store.BeginUpdateMessage(workOrder, TestContextToken))
        {
            Assert.Equal(0, factory.CountRows());
        }

        Assert.Equal(0, factory.CountRows());
        await store.SaveWorkOrderAsync(workOrder, TestContextToken);
        Assert.Equal(1, factory.CountRows());
    }

    [Fact]
    public async Task SimulatedCommitFailureRollsBackAndReleasesWriter()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IWorkOrderStore>();
        var workOrder = WorkOrderFactory.Create(Reading with { DbCommitError = true });
        using var transaction = await store.BeginUpdateMessage(workOrder, TestContextToken);
        await Assert.ThrowsAsync<DataStoreCommitException>(() => store.Commit(transaction));
        Assert.Equal(0, factory.CountRows());
        await store.SaveWorkOrderAsync(WorkOrderFactory.Create(Reading), TestContextToken);
        Assert.Equal(1, factory.CountRows());
    }

    [Fact]
    public async Task TransactionUpdateFailureReleasesWriter()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IWorkOrderStore>();
        await Assert.ThrowsAsync<DataStoreUpdateException>(() =>
            store.BeginUpdateMessage(WorkOrderFactory.Create(Reading with { DbUpdateError = true }), TestContextToken));
        Assert.Equal(0, factory.CountRows());
        await store.SaveWorkOrderAsync(WorkOrderFactory.Create(Reading), TestContextToken);
        Assert.Equal(1, factory.CountRows());
    }

    [Fact]
    public async Task TransactionCannotBeCommittedByAnotherStore()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var other = new DemoFactory();
        using var otherClient = other.CreateClient();
        var store = factory.Services.GetRequiredService<IWorkOrderStore>();
        var otherStore = other.Services.GetRequiredService<IWorkOrderStore>();
        using var transaction = await store.BeginUpdateMessage(WorkOrderFactory.Create(Reading), TestContextToken);
        await Assert.ThrowsAsync<ArgumentException>(() => otherStore.Commit(transaction));
        Assert.Equal(0, factory.CountRows());
        await store.Commit(transaction);
        Assert.Equal(1, factory.CountRows());
        Assert.Equal(0, other.CountRows());
    }

    [Fact]
    public async Task CancelledTransactionBeginDoesNotWrite()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IWorkOrderStore>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.BeginUpdateMessage(WorkOrderFactory.Create(Reading), new CancellationToken(canceled: true)));
        Assert.Equal(0, factory.CountRows());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SimulatedUpdateFailureDoesNotInsertOrRunLaterOperations(bool vendorError, bool commitError)
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/telemetry",
            Reading with { DbUpdateError = true, DbCommitError = commitError, VendorError = vendorError }, TestContextToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, factory.CountRows());
        Assert.Empty(factory.Vendor.Payloads);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContextToken));
        Assert.Equal("Data store update failed", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            [new OperationResult("Save work order", false)],
            Assert.IsType<OperationResult[]>(problem.RootElement.GetProperty("operations").Deserialize<OperationResult[]>(JsonSerializerOptions.Web)));

        using var next = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        Assert.Equal(1, factory.CountRows());
    }

    [Fact]
    public async Task SimulatedUpdateFailureUsesDatabaseAgnosticException()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<IWorkOrderStore>();
        var workOrder = WorkOrderFactory.Create(Reading with { DbUpdateError = true });

        await Assert.ThrowsAsync<DataStoreUpdateException>(
            () => store.SaveWorkOrderAsync(workOrder, TestContextToken));
        Assert.Equal(0, factory.CountRows());
    }

    [Fact]
    public async Task CommitErrorIsIgnoredWithoutExplicitTransaction()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/telemetry", Reading with { DbCommitError = true }, TestContextToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, factory.CountRows());
        var result = await response.Content.ReadFromJsonAsync<CreateWorkOrderResponse>(TestContextToken);
        Assert.NotNull(result);
        Assert.Equal(
            [new OperationResult("Save work order", true), new OperationResult("Notify vendor", true)],
            result.Operations);
        var saved = await client.GetFromJsonAsync<WorkOrder>(response.Headers.Location, TestContextToken);
        Assert.NotNull(saved);
        Assert.True(saved.SourceTelemetry.DbCommitError);
        using var notification = JsonDocument.Parse(Assert.Single(factory.Vendor.Payloads));
        Assert.False(notification.RootElement.TryGetProperty("dbUpdateError", out _));
        Assert.False(notification.RootElement.TryGetProperty("dbCommitError", out _));
    }

    [Fact]
    public async Task VendorErrorSelects503EndpointAndLeavesSavedWorkOrder()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/telemetry", Reading with { VendorError = true }, TestContextToken);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(new Uri("https://httpbin.org/status/503"), Assert.Single(factory.Vendor.Endpoints));
        Assert.True(factory.Vendor.SawSavedWorkOrder);
        Assert.Equal(1, factory.CountRows());
        using var notification = JsonDocument.Parse(Assert.Single(factory.Vendor.Payloads));
        Assert.False(notification.RootElement.TryGetProperty("vendorError", out _));
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContextToken));
        Assert.Equal(
            [new OperationResult("Save work order", true), new OperationResult("Notify vendor", false)],
            Assert.IsType<OperationResult[]>(problem.RootElement.GetProperty("operations").Deserialize<OperationResult[]>(JsonSerializerOptions.Web)));
        Assert.Equal(
            "The system is in an unknown, possibly inconsistent state. Verify the database and vendor system before retrying.",
            problem.RootElement.GetProperty("detail").GetString());
        var id = problem.RootElement.GetProperty("workOrderId").GetString();
        var saved = await client.GetFromJsonAsync<WorkOrder>($"/work-orders/{id}", TestContextToken);
        Assert.NotNull(saved);
        Assert.True(saved.SourceTelemetry.VendorError);

        using var next = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        Assert.Equal(new Uri("https://httpbin.org/post"), factory.Vendor.Endpoints[1]);
    }

    [Fact]
    public async Task SavesFullWorkOrderBeforePostingDifferentVendorPayload()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateWorkOrderResponse>(TestContextToken);
        Assert.NotNull(result);
        Assert.Equal(
            [new OperationResult("Save work order", true), new OperationResult("Notify vendor", true)],
            result.Operations);
        var workOrder = result.WorkOrder;
        Assert.Equal("Urgent", workOrder.Priority);
        Assert.Equal(["Inspect cooling system", "Replace brake pads", "Diagnose fault code P0217"], workOrder.Tasks);
        Assert.Equal(Reading.AlertId, workOrder.SourceTelemetry.AlertId);
        Assert.Equal(1, factory.CountRows());
        Assert.True(factory.Vendor.SawSavedWorkOrder);

        using var notification = JsonDocument.Parse(Assert.Single(factory.Vendor.Payloads));
        Assert.Equal(new Uri("https://httpbin.org/post"), Assert.Single(factory.Vendor.Endpoints));
        Assert.Equal(workOrder.WorkOrderId, notification.RootElement.GetProperty("workOrderId").GetString());
        Assert.Equal(Reading.AlertId, notification.RootElement.GetProperty("alertId").GetGuid());
        Assert.Equal(Reading.BusId, notification.RootElement.GetProperty("busId").GetString());
        Assert.False(notification.RootElement.TryGetProperty("sourceTelemetry", out _));
        Assert.False(notification.RootElement.TryGetProperty("engineTempC", out _));

        using var readResponse = await client.GetAsync(response.Headers.Location, TestContextToken);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var saved = await readResponse.Content.ReadFromJsonAsync<WorkOrder>(TestContextToken);
        Assert.NotNull(saved);
        Assert.Equal(workOrder.WorkOrderId, saved.WorkOrderId);
        Assert.Equal(Reading.EngineTempC, saved.SourceTelemetry.EngineTempC);
    }

    [Fact]
    public async Task RepeatedAlertCreatesTwoWorkOrdersAndTwoNotifications()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var first = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        using var second = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual(first.Headers.Location, second.Headers.Location);
        Assert.Equal(2, factory.CountRows());
        Assert.Equal(2, factory.Vendor.Payloads.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Found)]
    public async Task VendorFailureReturnsBadGatewayButLeavesWorkOrderSaved(HttpStatusCode vendorStatus)
    {
        using var factory = new DemoFactory();
        factory.Vendor.Status = vendorStatus;
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, factory.CountRows());
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContextToken));
        var id = problem.RootElement.GetProperty("workOrderId").GetString();
        using var saved = await client.GetAsync(new Uri($"/work-orders/{id}", UriKind.Relative), TestContextToken);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        factory.Vendor.Status = HttpStatusCode.OK;
        using var retry = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(2, factory.CountRows());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VendorConnectionFailureOrTimeoutLeavesWorkOrderSaved(bool timeout)
    {
        using var factory = new DemoFactory();
        factory.Vendor.Failure = timeout ? new TaskCanceledException("Simulated timeout") : new HttpRequestException("Simulated connection failure");
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(1, factory.CountRows());
    }

    [Fact]
    public async Task DatabaseFailureDoesNotNotifyVendor()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        factory.DropWorkOrders();

        using var response = await client.PostAsJsonAsync("/telemetry", Reading, TestContextToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(factory.Vendor.Payloads);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContextToken));
        var operations = problem.RootElement.GetProperty("operations").Deserialize<OperationResult[]>(JsonSerializerOptions.Web);
        Assert.NotNull(operations);
        Assert.Equal([new OperationResult("Save work order", false)], operations);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"alertId\":\"invalid\"}")]
    public async Task InvalidInputDoesNotWriteOrNotify(string json)
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/telemetry", UriKind.Relative), content, TestContextToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.CountRows());
        Assert.Empty(factory.Vendor.Payloads);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task BlankBusIdDoesNotWriteOrNotify(string? busId)
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/telemetry", Reading with { BusId = busId! }, TestContextToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.CountRows());
        Assert.Empty(factory.Vendor.Payloads);
    }

    [Fact]
    public async Task SavedWorkOrderSurvivesServiceRestart()
    {
        var directory = Directory.CreateTempSubdirectory("ctpdemos-");
        try
        {
            var path = Path.Combine(directory.FullName, "work-orders.db");
            Uri? location;
            using (var original = new DemoFactory(path))
            using (var first = original.CreateClient())
            using (var response = await first.PostAsJsonAsync("/telemetry", Reading, TestContextToken))
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                location = response.Headers.Location;
            }

            using var restarted = new DemoFactory(path);
            using var client = restarted.CreateClient();
            using var read = await client.GetAsync(location, TestContextToken);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal(1, restarted.CountRows());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task OpenApiAndMissingWorkOrderAreAvailable()
    {
        using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        using var missing = await client.GetAsync(new Uri("/work-orders/missing", UriKind.Relative), TestContextToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var specification = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContextToken);
        Assert.Equal(HttpStatusCode.OK, specification.StatusCode);
        var json = await specification.Content.ReadAsStringAsync(TestContextToken);
        Assert.Contains("/telemetry", json, StringComparison.Ordinal);
        Assert.Contains("\"vendorError\"", json, StringComparison.Ordinal);
    }

    private static CancellationToken TestContextToken => CancellationToken.None;

    [Fact]
    public void OperationReportPrintsOnlyReportedOutcomesInResponseOrder()
    {
        var report = Bus.Services.OperationReport.Read("""
            {"operations":[
                {"operation":"Notify vendor","succeeded":true},
                {"operation":"Additional operation","succeeded":true},
                {"operation":"Save work order","succeeded":false}
            ]}
            """);
        Assert.Equal(
            ["Succeeded: Notify vendor", "Succeeded: Additional operation", "Failed: Save work order"],
            report);
        Assert.Empty(Bus.Services.OperationReport.Read("""{"title":"Invalid request"}"""));
    }

    [Fact]
    public void OperationReportRejectsMalformedOutcomes()
    {
        Assert.ThrowsAny<JsonException>(() => Bus.Services.OperationReport.Read("not JSON"));
        Assert.Throws<KeyNotFoundException>(() => Bus.Services.OperationReport.Read(
            """{"operations":[{"operation":"Save work order"}]}"""));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitsForAsynchronousSaveBeforeNotifyingVendor(bool saveFails)
    {
        var store = new PendingStore();
        var vendor = new RecordingNotifier();
        var service = new DualWriteService(store, vendor);

        var pending = service.CreateAsync(Reading, TestContextToken);
        Assert.False(pending.IsCompleted);
        Assert.False(vendor.WasCalled);

        if (saveFails)
        {
            store.SaveCompletion.SetException(new IOException("Simulated database failure"));
            var error = await Assert.ThrowsAsync<OperationFailedException>(() => pending);
            Assert.IsType<IOException>(error.InnerException);
            Assert.Equal([new OperationResult("Save work order", false)], error.Operations);
            Assert.False(vendor.WasCalled);
        }
        else
        {
            store.SaveCompletion.SetResult();
            await pending.ConfigureAwait(true);
            Assert.True(vendor.WasCalled);
        }
    }

    private sealed class PendingStore : IWorkOrderStore
    {
        public TaskCompletionSource SaveCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Initialize() => throw new NotSupportedException();
        public Task SaveWorkOrderAsync(WorkOrder workOrder, CancellationToken ct) => SaveCompletion.Task;
        public WorkOrder? Find(string workOrderId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RecordingNotifier : IVendorNotifier
    {
        public bool WasCalled { get; private set; }

        public Task NotifyVendorAsync(WorkOrder workOrder, CancellationToken ct)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class DemoFactory : WebApplicationFactory<Program>
    {
        private readonly string? ownedDirectory;
        private bool disposed;

        public DemoFactory(string? existingPath = null)
        {
            if (existingPath is null)
            {
                ownedDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(ownedDirectory);
            }

            DatabasePath = existingPath ?? Path.Combine(ownedDirectory!, "work-orders.db");
            Vendor = new VendorHandler(this);
        }

        public string DatabasePath { get; }
        public VendorHandler Vendor { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["WorkOrders:DatabasePath"] = DatabasePath }));
            builder.ConfigureServices(services =>
                services.AddHttpClient<IVendorNotifier, VendorNotifier>()
                    .ConfigurePrimaryHttpMessageHandler(() => Vendor));
        }

        public SqliteConnection OpenDatabase()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
            connection.Open();
            return connection;
        }

        public int CountRows()
        {
            using var connection = OpenDatabase();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM WorkOrders;";
            return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        public void DropWorkOrders()
        {
            using var connection = OpenDatabase();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE WorkOrders;";
            command.ExecuteNonQuery();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed) return;
            disposed = true;
            base.Dispose(disposing);
            if (disposing)
            {
                Vendor.Dispose();
                if (ownedDirectory is not null) Directory.Delete(ownedDirectory, recursive: true);
            }
        }
    }

    private sealed class VendorHandler(DemoFactory factory) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public Exception? Failure { get; set; }
        public List<string> Payloads { get; } = [];
        public List<Uri?> Endpoints { get; } = [];
        public bool SawSavedWorkOrder { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Endpoints.Add(request.RequestUri);
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            SawSavedWorkOrder = factory.CountRows() > 0;
            if (Failure is not null) throw Failure;
            return new HttpResponseMessage(request.RequestUri == new Uri("https://httpbin.org/status/503")
                ? HttpStatusCode.ServiceUnavailable
                : Status);
        }
    }
}
