using System.Security.Cryptography;
using Bus.Messages;

namespace Bus.Services;

internal static class TelemetryGenerator
{
    private static readonly string[] KnownFaultCodes =
        ["P0128", "P0217", "P0300", "P0420", "C0035", "U0100"];

    public static BusTelemetry Create(string? busId, Guid? alertId)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            AlertId: alertId ?? Guid.NewGuid(),
            BusId: busId ?? $"B-{RandomNumberGenerator.GetInt32(1, 100)}",
            Timestamp: now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond)),
            OdometerKm: NextTenths(10_000, 400_000),
            EngineTempC: NextTenths(80, 120),
            BrakePadThicknessMm: NextTenths(2, 12),
            FaultCodes: NextFaultCodes());
    }

    // Returns a value in [min, max) rounded to one decimal place.
    private static double NextTenths(int min, int max) =>
        RandomNumberGenerator.GetInt32(min * 10, max * 10) / 10.0;

    // Most readings carry no fault codes; the rest carry one or two distinct codes.
    private static string[]? NextFaultCodes()
    {
        if (RandomNumberGenerator.GetInt32(100) >= 30) return null;

        var count = RandomNumberGenerator.GetInt32(1, 3);
        var codes = KnownFaultCodes.ToArray();
        RandomNumberGenerator.Shuffle(codes.AsSpan());
        return codes[..count];
    }
}
