namespace WorkOrderService;

public static class WorkOrderFactory
{
    public static WorkOrder Create(BusTelemetry t)
    {
        var tasks = new List<string>();
        var priority = "Routine";

        if (t.EngineTempC >= 110) { tasks.Add("Inspect cooling system"); priority = "Urgent"; }
        if (t.BrakePadThicknessMm <= 4) { tasks.Add("Replace brake pads"); priority = "Urgent"; }
        foreach (var code in t.FaultCodes ?? []) { tasks.Add($"Diagnose fault code {code}"); if (priority == "Routine") priority = "High"; }
        if (tasks.Count == 0) tasks.Add("Perform scheduled inspection");

        return new WorkOrder($"WO-{Guid.NewGuid():N}", t.BusId, DateTimeOffset.UtcNow, priority, tasks, t);
    }
}
