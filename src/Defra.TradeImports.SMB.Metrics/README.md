# Defra.TradeImports.SMB.Metrics

A lightweight metrics and logging instrumentation library for **SlimMessageBus** consumers, built on **.NET OpenTelemetry / System.Diagnostics.Metrics**.

This package provides:
- Standardised consumer-level metrics (count, duration, faults, warnings, concurrency)
- A reusable consumer interceptor
- Strongly-typed access to message bus headers for resource context

---

## Overview

The library instruments message consumption using a **SlimMessageBus consumer interceptor**. Metrics are emitted via `IMeterFactory` and enriched with consistent tags such as queue name, consumer type, resource type, and service name.

The design is intentionally non-intrusive: consumers require no code changes beyond registering the interceptor and setting headers.

---

## Key Components

### MetricsInterceptor\<TMessage>

Implements `IConsumerInterceptor<TMessage>` and wraps message handling to record:

- Total messages consumed
- Active consumers (in-progress)
- Processing duration (histogram)
- Faults and warnings (with exception type)
- Structured log events for warn and fault scenarios

`HttpRequestException` with status code **409 (Conflict)** is treated as a *warning* rather than a fault.

---

### ConsumerMetrics

Encapsulates all metric instruments.

| Metric Name | Type | Description |
|------------|------|-------------|
| MessagingConsume | Counter | Total messages consumed |
| MessagingConsumeActive | Counter | Consumers currently in progress |
| MessagingConsumeErrors | Counter | Message processing faults |
| MessagingConsumeWarnings | Counter | Recoverable warnings |
| MessagingConsumeDuration | Histogram | Time spent consuming a message (ms) |

All metrics are enriched with consistent tags for observability.

---

### ConsumerContextExtensions

Extension methods on `IConsumerContext` for extracting message header metadata:

- `GetResourceType()`
- `GetSubResourceType()`
- `GetResourceId()`

These values are propagated into metrics and logs.

---

### MessageBusHeaders

Defines canonical message header names:

- `ResourceType`
- `SubResourceType`
- `ResourceId`

Using constants avoids string duplication and enforces cross-service consistency.

---

## Metric Tags

Every metric includes the following tags:

| Tag | Description |
|----|-------------|
| ServiceName | Current process name |
| QueueName | Message path / queue |
| ConsumerType | Consumer class name |
| ResourceType | Logical domain resource |
| SubResourceType | Optional sub-classification |
| ExceptionType | Fault / warning only |

---

## Usage

### Register Metrics

```csharp
services.AddSingleton(sp =>
    new ConsumerMetrics(
        sp.GetRequiredService<IMeterFactory>(),
        meterName: "Defra.TradeImports.Messaging"
    ));
```





## Logging Behaviour

Two structured log events are emitted:

Event ID	Scenario	Level
1001	Recoverable warning (HTTP 409)	Information
1002	Unhandled exception	Information

Logs include consumer name, resource ID, resource type, and subtype.

## Design Principles

- `Low allocation / high throughput`

- `No reflection`

- `No consumer code coupling`

- `Explicit domain tagging`

- `Production-safe defaults`
