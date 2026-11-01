# DirectToTopic.SingleConsumer – bus telemetry to work order

An Aspire AppHost starts a Kafka container (with Kafka UI) and a C# worker service. The worker reads bus telemetry
from the `bus-telemetry` topic and writes a maintenance work order as a commented YAML file to `DirectToTopic.SingleConsumer/work-orders/`.

## Run
Requires the .NET 10 SDK and a container runtime (Docker/Podman).

    dotnet run --project DirectToTopic.SingleConsumer/AppHost

Open the Aspire dashboard, then open Kafka UI and produce a message to `bus-telemetry` (see the contract below). The producer sends the internal alert's stable `alertId` GUID, not a WorkOrderId; reuse the same alert ID on retries so the service creates at most one work order for that alert.

A file such as `DirectToTopic.SingleConsumer/work-orders/Alert-6ba7b8109dad11d180b400c04fd430c8.yaml` appears. The telemetry producer is outside this demo's scope.

## Kafka data
Kafka data is stored in the Docker volume `ctpdemos-kafka-data`, so topics and messages persist between runs. Remove the volume manually when you no longer need this data:

    docker volume rm ctpdemos-kafka-data

## Message contract
The telemetry contract is documented in [asyncapi.yaml](asyncapi.yaml) (AsyncAPI 3.0): topic `bus-telemetry`,
JSON payload. A sample message, also included in that document:

    {"alertId":"6ba7b810-9dad-11d1-80b4-00c04fd430c8","busId":"B-42","timestamp":"2026-01-15T08:30:00Z","odometerKm":182340.5,"engineTempC":115,"brakePadThicknessMm":3.2,"faultCodes":["P0217"]}

The consumer-side model is `WorkOrderService/BusTelemetry.cs`; keep it in sync with the document.
