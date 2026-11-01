using System.Text.Json.Serialization;

namespace WorkOrderService;

public record BusTelemetry(
    [property: JsonPropertyName("alertId"), JsonRequired] Guid AlertId,
    [property: JsonPropertyName("busId")] string BusId,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("odometerKm")] double OdometerKm,
    [property: JsonPropertyName("engineTempC")] double EngineTempC,
    [property: JsonPropertyName("brakePadThicknessMm")] double BrakePadThicknessMm,
    [property: JsonPropertyName("faultCodes")] string[]? FaultCodes);
