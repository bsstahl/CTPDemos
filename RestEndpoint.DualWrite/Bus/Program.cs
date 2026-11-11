using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using Bus.Messages;
using Bus.Services;

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
    Description = "Alert ID (GUID) to send. A new GUID is used if omitted. Reusing an ID still creates another work order.",
};

Option<string> urlOption = new("--url")
{
    Description = "Full telemetry endpoint URL. Use the work-order-service URL from Aspire with /telemetry appended.",
    DefaultValueFactory = _ => "http://localhost:5080/telemetry",
};
urlOption.Validators.Add(result =>
{
    if (!Uri.TryCreate(result.GetValueOrDefault<string>(), UriKind.Absolute, out var url)
        || url.Scheme is not ("http" or "https"))
        result.AddError("--url must be an absolute HTTP or HTTPS URL.");
});

Option<bool> vendorErrorOption = new("--vendorError")
{
    Description = "Ask the service to POST the vendor notification to https://httpbin.org/status/503.",
};

Option<bool> dbUpdateErrorOption = new("--dbUpdateError")
{
    Description = "Simulate a datastore update failure.",
};

Option<bool> dbCommitErrorOption = new("--dbCommitError")
{
    Description = "Simulate a transaction commit failure. Ignored when no explicit transaction is used.",
};

RootCommand rootCommand = new("Posts one random bus telemetry message to the REST dual-write service.")
{
    idOption,
    alertIdOption,
    urlOption,
    vendorErrorOption,
    dbUpdateErrorOption,
    dbCommitErrorOption,
};

rootCommand.SetAction(async (parseResult, ct) =>
{
    var telemetry = TelemetryGenerator.Create(
        parseResult.GetValue(idOption),
        parseResult.GetValue(alertIdOption)) with
    {
        VendorError = parseResult.GetValue(vendorErrorOption),
        DbUpdateError = parseResult.GetValue(dbUpdateErrorOption),
        DbCommitError = parseResult.GetValue(dbCommitErrorOption),
    };
    var url = new Uri(parseResult.GetValue(urlOption)!, UriKind.Absolute);

    using var handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
    try
    {
        using var response = await client.PostAsJsonAsync(
            url, telemetry, BusTelemetryJsonContext.Default.BusTelemetry, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var report = OperationReport.Read(body);
        foreach (var line in report)
        {
            await parseResult.InvocationConfiguration.Output.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
        }
        if (!response.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(body);
            var detail = document.RootElement.TryGetProperty("detail", out var errorDetail)
                ? errorDetail.GetString()
                : report.Count == 0 ? body : null;
            await parseResult.InvocationConfiguration.Error.WriteLineAsync(
                $"Request failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {detail}".AsMemory(), ct).ConfigureAwait(false);
            return 1;
        }

        await parseResult.InvocationConfiguration.Output.WriteLineAsync(
            $"Request completed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}".AsMemory(), ct).ConfigureAwait(false);
        return 0;
    }
    catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException
        || ex is OperationCanceledException && !ct.IsCancellationRequested)
    {
        await parseResult.InvocationConfiguration.Error.WriteLineAsync(
            $"Request to {url} failed: {ex.Message}. The system is in an unknown, possibly inconsistent state. Verify the database and vendor system before retrying.".AsMemory(), ct).ConfigureAwait(false);
        return 1;
    }
});

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
