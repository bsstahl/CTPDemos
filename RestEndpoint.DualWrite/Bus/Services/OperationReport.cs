using System.Text.Json;

namespace Bus.Services;

internal static class OperationReport
{
    public static IReadOnlyList<string> Read(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("operations", out var operations))
            return [];

        var lines = new List<string>();
        foreach (var operation in operations.EnumerateArray())
        {
            var name = operation.GetProperty("operation").GetString();
            if (string.IsNullOrWhiteSpace(name)) throw new JsonException("An operation name is required.");
            var succeeded = operation.GetProperty("succeeded").GetBoolean();
            lines.Add($"{(succeeded ? "Succeeded" : "Failed")}: {name}");
        }

        return lines;
    }
}
