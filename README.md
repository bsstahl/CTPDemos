# CTPDemos

Code samples and implementations of the **Consume-Transform-Produce** pattern

## Purpose

Consume-Transform-Produce (CTP) is an architectural pattern in which a service receives an input, applies business logic, and produces a result, often resulting in one or more side effects such as emitted messages, API calls, or data store updates.

This pattern underlies a large percentage of modern applications. From ASP.NET request handling and Kafka consumers to serverless functions and data pipelines, many systems follow the same fundamental flow: receive work, process it, and generate outcomes.

As such, Consume-Transform-Produce can be viewed as a unifying abstraction for many familiar architectural patterns, including Request/Response, Producer/Consumer, Event Processing, and ETL. While the terminology is not universally used, Consume-Transform-Produce describes a fundamental flow that appears throughout modern software systems regardless of language, framework, or deployment model.

## Demos

Each demo in this repo is a runnable example of the pattern in a different configuration, such as how the data enters the system and how many consumers process it.

Demos are orchestrated with [Microsoft Aspire](https://aspire.dev), so you can run one with a single command. You need the .NET 11 SDK; demos that use containers also need a container runtime (Docker or Podman). Each demo folder has its own README with run instructions.

| Demo Docs | Solution File | Description |
|-----------|---------------|-------------|
| [DirectToTopic.SingleConsumer](DirectToTopic.SingleConsumer/README.md) | [DirectToTopic.SingleConsumer.Slnx](DirectToTopic.SingleConsumer.slnx) | A bus publishes telemetry directly to a Kafka topic. A single C# service consumes it and transforms each message into a maintenance work order, written as a commented YAML file. The message contract is documented in an [AsyncAPI](DirectToTopic.SingleConsumer/asyncapi.yaml) file. |
| [HttpProxy.SingleConsumer](HttpProxy.SingleConsumer/README.md) | [HttpProxy.SingleConsumer.slnx](HttpProxy.SingleConsumer.slnx) | The same consumer and work-order output as [DirectToTopic.SingleConsumer](DirectToTopic.SingleConsumer/README.md), but the bus sends telemetry over HTTP to Kafka REST Proxy, which publishes it to Kafka. |
| [RestEndpoint.DualWrite](RestEndpoint.DualWrite/README.md) | [RestEndpoint.DualWrite.slnx](RestEndpoint.DualWrite.slnx) | A REST service saves a work order in SQLite, then POSTs a notification to a vendor stand-in. The separate writes demonstrate partial failures and duplicate work orders on retries. Includes an HTTP bus CLI. |
