# Defra.TradeImports.EmfExporter

A lightweight **AWS CloudWatch EMF (Embedded Metric Format)** exporter for `.NET` applications using `System.Diagnostics.Metrics`.

This library listens to metrics emitted via a specific `Meter` and publishes them to **AWS CloudWatch** using the AWS EMF logger, enabling high-cardinality, dimension-rich metrics without manual CloudWatch API calls.

---

## Overview

`Defra.TradeImports.EmfExporter` bridges **OpenTelemetry-style .NET metrics** (`Meter`, `Counter`, `Histogram`, etc.) to **AWS CloudWatch EMF**.

It works by:
1. Subscribing to instruments from a named `Meter`
2. Translating metric measurements into EMF records
3. Mapping metric tags into CloudWatch dimensions
4. Automatically flushing metrics via the EMF logger

The exporter is designed for **ASP.NET Core** services running in AWS (ECS, EKS, Lambda, EC2) but can also be conditionally enabled or disabled via configuration.

---

## Key Components

### EmfExporter

A static exporter that configures a `MeterListener` and forwards metric measurements to CloudWatch EMF.

**Responsibilities:**
- Subscribes only to instruments from a specific meter name
- Converts metric values to EMF-compatible numeric values
- Maps metric tags to CloudWatch dimensions
- Applies the configured AWS namespace
- Fails safely (logs errors, does not crash the application)

Supported measurement types:
- `int`
- `long`
- `double`

---

### EmfExportExtensions

ASP.NET Core startup extension for enabling the exporter.

Responsibilities:
- Reads EMF configuration from `IConfiguration`
- Conditionally enables EMF based on environment settings
- Resolves and validates the CloudWatch namespace
- Initializes the exporter with the application `ILoggerFactory`

---

## Configuration

The exporter is controlled via environment variables or configuration values.

| Key | Description | Default |
|---|---|---|
| `AWS_EMF_ENABLED` | Enables or disables EMF exporting | `true` |
| `AWS_EMF_NAMESPACE` | CloudWatch namespace for metrics | **Required** |
| `AWS_EMF_ENVIRONMENT` | Environment name (e.g. Local) | Optional |

### Local Development Behaviour

If:
- `AWS_EMF_ENABLED = true`
- `AWS_EMF_ENVIRONMENT = Local`
- `AWS_EMF_NAMESPACE` is **not set**

Then the namespace defaults to the **entry assembly namespace**.

If no namespace can be resolved, startup will fail to prevent silent misconfiguration.

---

## Usage

### Emit Metrics

```csharp
var meter = new Meter("Defra.TradeImports.Messaging");

var counter = meter.CreateCounter<long>(
    "MessagingConsume",
    unit: "count",
    description: "Number of messages consumed"
);

counter.Add(1, new KeyValuePair<string, object?>("QueueName", "imports"));
