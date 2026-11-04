using Defra.TradeImports.Tracing;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Primitives;

namespace Defra.TradeImports.Messaging.Consuming.Middleware;

/// <summary>
/// Takes the trace id from the incoming message (or starts a new one), sets it as the current
/// <see cref="ITraceContext"/>, and puts it where the header propagation handler expects it, so
/// outbound HTTP calls and re-published messages carry it on.
/// </summary>
public class TracingConsumeMiddleware(
    ITraceContextAccessor traceContextAccessor,
    HeaderPropagationValues headerPropagationValues,
    ILogger<TracingConsumeMiddleware> logger
) : IConsumeMiddleware
{
    public async Task InvokeAsync(
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default
    )
    {
        var traceId = context.GetTraceId();

        traceContextAccessor.Context = new TraceContext { TraceId = traceId };

        var headers = headerPropagationValues.Headers ??= new Dictionary<string, StringValues>(
            StringComparer.OrdinalIgnoreCase
        );
        headers[MessagingHeaders.TraceId] = traceId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = traceId }))
        {
            await next();
        }
    }
}
