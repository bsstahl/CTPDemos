# CTPDemos
Code samples and implementations of the Consume-Transform-Produce pattern

## Purpose
Consume-Transform-Produce (CTP) is a pattern in which a service consumes a message, transforms it, and produces a result, which may be another message or a stored artifact. Each demo in this repo is a runnable example of the pattern in a different configuration, such as how the data enters the system and how many consumers process it.

Demos are orchestrated with [.NET Aspire](https://aspire.dev), so you can run one with a single command. You need the .NET 11 SDK and a container runtime (Docker or Podman). Each demo folder has its own README with run instructions.

## Demos

| Demo Docs | Solution File | Description |
|-----------|---------------|-------------|
| [DirectToTopic.SingleConsumer](DirectToTopic.SingleConsumer/README.md) | [DirectToTopic.SingleConsumer.Slnx](DirectToTopic.SingleConsumer.slnx) | A bus publishes telemetry directly to a Kafka topic. A single C# service consumes it and transforms each message into a maintenance work order, written as a commented YAML file. The message contract is documented in an [AsyncAPI](DirectToTopic.SingleConsumer/asyncapi.yaml) file. |
| [HttpProxy.SingleConsumer](HttpProxy.SingleConsumer/README.md) | [HttpProxy.SingleConsumer.slnx](HttpProxy.SingleConsumer.slnx) | The same consumer and work-order output, but the bus sends telemetry over HTTP to Kafka REST Proxy, which publishes it to Kafka. |
