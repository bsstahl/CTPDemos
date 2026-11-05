using System.CommandLine;
using Bus.Services;
using Confluent.Kafka;

Option<string?> idOption = new("--id")
{
    Description = "Bus ID to send (for example B-42). A random bus ID is used if omitted.",
};
idOption.Validators.Add(result =>
{
    if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string?>()))
        result.AddError("--id cannot be empty.");
});

Option<Guid?> alertIdOption = new("--alertid")
{
    Description = "Alert ID (GUID) to send. Reuse one to simulate a retry of the same alert. A new GUID is used if omitted.",
};

RootCommand rootCommand = new(
    $"Publishes one bus telemetry message with random readings to the '{TelemetryPublisher.Topic}' Kafka topic at {TelemetryPublisher.BootstrapServers}.")
{
    idOption,
    alertIdOption,
};

rootCommand.SetAction(async (parseResult, ct) =>
{
    var telemetry = TelemetryGenerator.Create(
        parseResult.GetValue(idOption),
        parseResult.GetValue(alertIdOption));

    try
    {
        var result = await TelemetryPublisher.PublishAsync(telemetry, ct).ConfigureAwait(false);
        await parseResult.InvocationConfiguration.Output.WriteLineAsync(
            $"Published to {result.TopicPartitionOffset}: {result.Message.Value}".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }
    catch (ProduceException<string, string> ex)
    {
        await parseResult.InvocationConfiguration.Error.WriteLineAsync(
            $"Failed to publish to {TelemetryPublisher.Topic}: {ex.Error.Reason}".AsMemory(), ct).ConfigureAwait(false);
        return 1;
    }
});

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
