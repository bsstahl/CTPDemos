# DirectToTopic.SingleConsumer – bus telemetry to work order

An Aspire AppHost starts a Kafka container (with Kafka UI) and a C# worker service. The worker reads bus telemetry
from the `bus-telemetry` topic and writes a maintenance work order as a commented YAML file to `DirectToTopic.SingleConsumer/work-orders/`.

## Run
Requires the .NET 10 SDK and a container runtime (Docker/Podman).

    dotnet run --project DirectToTopic.SingleConsumer/AppHost

Open the Aspire dashboard, then open Kafka UI and produce a message to `bus-telemetry` (see the contract below).

A file such as `DirectToTopic.SingleConsumer/work-orders/WO-B-42-20260115083000.yaml` appears. The telemetry producer is outside this demo's scope.

## Message contract
The telemetry contract is documented in [asyncapi.yaml](asyncapi.yaml) (AsyncAPI 3.0): topic `bus-telemetry`,
JSON payload. A sample message, also included in that document:

    {"busId":"B-42","timestamp":"2026-01-15T08:30:00Z","odometerKm":182340.5,"engineTempC":115,"brakePadThicknessMm":3.2,"faultCodes":["P0217"]}

The consumer-side model is `WorkOrderService/BusTelemetry.cs`; keep it in sync with the document.
