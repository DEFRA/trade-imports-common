
---

**`src/Defra.TradeImports.SMB.Tracing/README.md`**

```markdown
# Defra.TradeImports.SMB.Tracing

A [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) consumer interceptor that propagates distributed trace context across message consumers.

## Overview

`TraceContextInterceptor` extracts a trace ID from incoming message headers (or generates a new one) and:

1. Populates `ITraceContextAccessor` so the trace ID is available throughout the consumer pipeline.
2. Injects the trace ID into `HeaderPropagationValues` so it is forwarded on any outbound HTTP calls made by the consumer.

Structured log messages are emitted for message start, 409 Conflict, and unhandled exceptions.

## Usage

```csharp
services.AddTraceContextInterceptor();