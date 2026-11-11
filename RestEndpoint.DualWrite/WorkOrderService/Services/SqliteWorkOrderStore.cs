using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WorkOrderService.Extensions;
using WorkOrderService.Messages;

namespace WorkOrderService.Services;

internal sealed class SqliteWorkOrderStore : IWorkOrderStore
{
    private readonly string connectionString;
    private readonly ILogger<SqliteWorkOrderStore> logger;

    public SqliteWorkOrderStore(IConfiguration configuration, IHostEnvironment environment, ILogger<SqliteWorkOrderStore> logger)
    {
        var path = Path.GetFullPath(
            configuration["WorkOrders:DatabasePath"] ?? Path.Combine("..", "data", "work-orders.db"),
            environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        this.logger = logger;
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS WorkOrders (
                WorkOrderId TEXT PRIMARY KEY NOT NULL,
                AlertId TEXT NOT NULL,
                BusId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                Priority TEXT NOT NULL,
                Tasks TEXT NOT NULL,
                SourceTelemetry TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
        Justification = "Microsoft.Data.Sqlite async methods execute synchronously; the task-based contract supports other database implementations.")]
    public Task SaveWorkOrderAsync(WorkOrder workOrder, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        InsertWorkOrder(connection, null, workOrder);
        logger.WorkOrderSaved(workOrder.WorkOrderId, workOrder.SourceTelemetry.AlertId);
        return Task.CompletedTask;
    }

    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method",
        Justification = "Microsoft.Data.Sqlite does not support asynchronous I/O.")]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The returned transaction handle owns the connection; the failure path disposes it locally.")]
    public Task<Transaction> BeginUpdateMessage(WorkOrder workOrder, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var connection = OpenConnection();
        SqliteTransaction? transaction = null;
        try
        {
            transaction = connection.BeginTransaction();
            InsertWorkOrder(connection, transaction, workOrder);
            var pendingTransaction = transaction;
            return Task.FromResult(new Transaction(this, () =>
            {
                if (workOrder.SourceTelemetry.DbCommitError)
                    throw new DataStoreCommitException("Simulated data store commit failure.");
                pendingTransaction.Commit();
                logger.WorkOrderSaved(workOrder.WorkOrderId, workOrder.SourceTelemetry.AlertId);
            }, () =>
            {
                try
                {
                    pendingTransaction.Dispose();
                }
                finally
                {
                    connection.Dispose();
                }
            }));
        }
        catch
        {
            try
            {
                transaction?.Dispose();
            }
            finally
            {
                connection.Dispose();
            }
            throw;
        }
    }

    public Task Commit(Transaction trx)
    {
        ArgumentNullException.ThrowIfNull(trx);
        trx.Commit(this);
        return Task.CompletedTask;
    }

    private static void InsertWorkOrder(SqliteConnection connection, SqliteTransaction? transaction, WorkOrder workOrder)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO WorkOrders (WorkOrderId, AlertId, BusId, CreatedAt, Priority, Tasks, SourceTelemetry)
            VALUES ($id, $alertId, $busId, $createdAt, $priority, $tasks, $telemetry);
            """;
        command.Parameters.AddWithValue("$id", workOrder.WorkOrderId);
        command.Parameters.AddWithValue("$alertId", workOrder.SourceTelemetry.AlertId.ToString("D"));
        command.Parameters.AddWithValue("$busId", workOrder.BusId);
        command.Parameters.AddWithValue("$createdAt", workOrder.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$priority", workOrder.Priority);
        command.Parameters.AddWithValue("$tasks", JsonSerializer.Serialize(workOrder.Tasks, JsonSerializerOptions.Web));
        command.Parameters.AddWithValue("$telemetry", JsonSerializer.Serialize(workOrder.SourceTelemetry, JsonSerializerOptions.Web));
        if (workOrder.SourceTelemetry.DbUpdateError)
            throw new DataStoreUpdateException("Simulated data store update failure.");

        command.ExecuteNonQuery();
    }

    public WorkOrder? Find(string workOrderId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT WorkOrderId, BusId, CreatedAt, Priority, Tasks, SourceTelemetry
            FROM WorkOrders WHERE WorkOrderId = $id;
            """;
        command.Parameters.AddWithValue("$id", workOrderId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new WorkOrder(
            reader.GetString(0),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            reader.GetString(3),
            JsonSerializer.Deserialize<List<string>>(reader.GetString(4), JsonSerializerOptions.Web)
                ?? throw new JsonException("Saved work order tasks cannot be null."),
            JsonSerializer.Deserialize<BusTelemetry>(reader.GetString(5), JsonSerializerOptions.Web)
                ?? throw new JsonException("Saved source telemetry cannot be null."));
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
