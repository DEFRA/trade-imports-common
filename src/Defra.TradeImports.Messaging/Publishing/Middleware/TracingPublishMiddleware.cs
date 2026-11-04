using Defra.TradeImports.Messaging.Extensions;
using Defra.TradeImports.Tracing;
using Microsoft.AspNetCore.HeaderPropagation;

namespace Defra.TradeImports.Messaging.Publishing.Middleware;

/// <summary>
/// Carries the CDP trace id onto the published message: the current <see cref="ITraceContext"/>
/// (set by consumers and scheduled jobs), or else the trace header propagated from the current HTTP
/// request.
/// </summary>
public class TracingPublishMiddleware(
    ITraceContextAccessor traceContextAccessor,
    HeaderPropagationValues headerPropagationValues
) : IPublishMiddleware
{
    public Task InvokeAsync(
        PublishContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default
    )
    {
        var traceId = traceContextAccessor.Context?.TraceId;

        if (
            string.IsNullOrEmpty(traceId)
            && headerPropagationValues.Headers?.TryGetValue(
                MessagingHeaders.TraceId,
                out var header
            ) == true
        )
            traceId = header.ToString();

        context.SetTraceId(traceId);
        return next();
    }
}
