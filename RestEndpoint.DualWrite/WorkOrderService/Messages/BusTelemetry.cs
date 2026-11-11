using System.Text.Json.Serialization;

namespace WorkOrderService.Messages;

/// <summary>Bus telemetry with optional switches for demonstrating operation failures.</summary>
internal sealed record BusTelemetry(
    [property: JsonPropertyName("alertId"), JsonRequired] Guid AlertId,
    [property: JsonPropertyName("busId"), JsonRequired] string BusId,
    [property: JsonPropertyName("timestamp"), JsonRequired] DateTimeOffset Timestamp,
    [property: JsonPropertyName("odometerKm"), JsonRequired] double OdometerKm,
    [property: JsonPropertyName("engineTempC"), JsonRequired] double EngineTempC,
    [property: JsonPropertyName("brakePadThicknessMm"), JsonRequired] double BrakePadThicknessMm,
    [property: JsonPropertyName("faultCodes")] string[]? FaultCodes,
    [property: JsonPropertyName("vendorError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool VendorError = false,
    [property: JsonPropertyName("dbUpdateError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool DbUpdateError = false,
    [property: JsonPropertyName("dbCommitError"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool DbCommitError = false);
