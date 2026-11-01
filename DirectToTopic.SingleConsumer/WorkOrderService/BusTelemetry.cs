using System.Text.Json.Serialization;

namespace WorkOrderService;

public record BusTelemetry(
    [property: JsonPropertyName("alertId"), JsonRequired] Guid AlertId,
    [property: JsonPropertyName("busId"), JsonRequired] string BusId,
    [property: JsonPropertyName("timestamp"), JsonRequired] DateTimeOffset Timestamp,
    [property: JsonPropertyName("odometerKm"), JsonRequired] double OdometerKm,
    [property: JsonPropertyName("engineTempC"), JsonRequired] double EngineTempC,
    [property: JsonPropertyName("brakePadThicknessMm"), JsonRequired] double BrakePadThicknessMm,
    [property: JsonPropertyName("faultCodes")] string[]? FaultCodes);
