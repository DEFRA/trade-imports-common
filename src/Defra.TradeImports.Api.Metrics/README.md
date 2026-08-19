# Defra.TradeImports.Api.Metrics

This library provides an abstraction for the collection of HTTP Request Metrics in an API Project via a Nuget package.

It collects metrics which include:

* A count of the number of successfully handled HTTP Requests
  * Including the following dimensions:
    * Request path
    * Request method
    * Response status code
    * Request duration
* A count of the number of faulted HTTP Requests
  * Including the following dimensions:
    * Request path
    * Request method
    * Response status code

Note - this package on its own does not publish metrics. Please refer to the Defra.TradeImports.EmfExporter package, in the DEFRA trade-imports-common repository, for info on publishing metrics via AWS CloudWatch EMF.

## Configuration

Configuration is bound to the application's configuration section `ApiMetrics`.  
You must provide, at least, a configuration value for the `MeterName`.  
The `IgnoredPathPrefixes` allows for the configuration of any URL path prefixes that should be excluded from the metrics.

### Example `appsettings.json`

```json
{
  "ApiMetrics": {
    "MeterName": "Defra.TradeGateway.Api",
    "IgnoredPathPrefixes": [ "/favicon", "/redoc", "/.well-known/openapi" ]
  }
}
```

### Usage

1. Add a reference to this Nuget package:

```shell
dotnet package add Defra.TradeImports.Api.Metrics
```

2. Add configuration as per above example
3. Register the API Metrics components in DI using the IServiceCollection extension method:

```csharp
builder.Services.AddApiMetrics()
```

4. Add the Metrics middleware to your applications request pipeline:

```csharp
app.UseMiddleware<ApiMetricsMiddleware>();
```