
---

**`src/Defra.TradeImports.Tracing/README.md`**

```markdown
# Defra.TradeImports.Tracing

Provides distributed trace context propagation for ASP.NET Core applications.

## Overview

Manages a per-request `TraceId` via `ITraceContextAccessor`, making it available throughout the request pipeline. Integrates with ASP.NET Core header propagation so the trace ID is forwarded on outbound HTTP calls.

## Usage

```csharp
services.AddTraceContextAccessor(configuration);