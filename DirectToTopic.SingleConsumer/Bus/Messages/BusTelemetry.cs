using System.Text.Json.Serialization;

namespace Bus.Messages;

// Producer-side model of the contract in asyncapi.yaml; keep it in sync with that document.
internal sealed record BusTelemetry(
    [property: JsonPropertyName("alertId")] Guid AlertId,
    [property: JsonPropertyName("busId")] string BusId,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("odometerKm")] double OdometerKm,
    [property: JsonPropertyName("engineTempC")] double EngineTempC,
    [property: JsonPropertyName("brakePadThicknessMm")] double BrakePadThicknessMm,
    [property: JsonPropertyName("faultCodes")] IReadOnlyList<string>? FaultCodes);

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BusTelemetry))]
internal sealed partial class BusTelemetryJsonContext : JsonSerializerContext;
