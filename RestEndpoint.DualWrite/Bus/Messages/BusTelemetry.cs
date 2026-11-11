using System.Text.Json.Serialization;

namespace Bus.Messages;

internal sealed record BusTelemetry(
    [property: JsonPropertyName("alertId")] Guid AlertId,
    [property: JsonPropertyName("busId")] string BusId,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("odometerKm")] double OdometerKm,
    [property: JsonPropertyName("engineTempC")] double EngineTempC,
    [property: JsonPropertyName("brakePadThicknessMm")] double BrakePadThicknessMm,
    [property: JsonPropertyName("faultCodes")] IReadOnlyList<string>? FaultCodes,
    [property: JsonPropertyName("vendorError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool VendorError = false,
    [property: JsonPropertyName("dbUpdateError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool DbUpdateError = false,
    [property: JsonPropertyName("dbCommitError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool DbCommitError = false);

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BusTelemetry))]
internal sealed partial class BusTelemetryJsonContext : JsonSerializerContext;
