using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorkOrderService;

public class WorkOrderWriter(string directory)
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public async Task<string> WriteAsync(WorkOrder order, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{order.WorkOrderId}.yaml");

        var sb = new StringBuilder()
            .AppendLine($"# Bus maintenance work order {order.WorkOrderId}")
            .AppendLine("# Generated automatically from bus telemetry received on Kafka.")
            .AppendLine("# Priority: Urgent = safety-related finding, High = fault codes reported, Routine = no findings.")
            .AppendLine("# 'sourceTelemetry' is the unmodified data that triggered this order.")
            .Append(_serializer.Serialize(order));

        await File.WriteAllTextAsync(path, sb.ToString(), ct);
        return path;
    }
}
