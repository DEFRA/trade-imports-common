using Defra.TradeImports.Tracing;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;
using System.Net;

namespace Defra.TradeImports.SMB.Tracing;

public class TraceContextInterceptor<TMessage>(
    IOptions<TraceHeader> traceHeader,
    ITraceContextAccessor traceContextAccessor,
    HeaderPropagationValues headerPropagationValues,
    ILogger<TraceContextInterceptor<TMessage>> logger
) : IConsumerInterceptor<TMessage>
{
    public async Task<object> OnHandle(TMessage message, Func<Task<object>> next, IConsumerContext context)
    {
	    var messageId = context.GetMessageId();
	    var resourceId = context.GetResourceId();
		logger.ProcessingMessage(messageId, resourceId);

		// Setting the trace context will take either the trace ID from the incoming
		// message headers or it will start a new trace ID that may be propagated onwards
		// to any nested HTTP calls or further message publishing
		traceContextAccessor.Context = new TraceContext
        {
            TraceId = context.Headers.GetTraceId(traceHeader.Value.Name) ?? Guid.NewGuid().ToString("N"),
        };

        // As per the middleware implementation for header propagation, the following sets
        // the headerPropagationValues.Headers value so it can be used by any configured
        // HTTP handler
        var headers = headerPropagationValues.Headers ??= new Dictionary<string, StringValues>(
            StringComparer.OrdinalIgnoreCase
        );

        headers.Add(traceHeader.Value.Name, traceContextAccessor.Context.TraceId);

		try
		{
			return await next();
		}
		catch (HttpRequestException httpRequestException)
			when (httpRequestException.StatusCode == HttpStatusCode.Conflict)
		{
			logger.OnConflict(httpRequestException, messageId, resourceId);
			throw;
		}
		catch (Exception exception)
		{
			logger.OnException(exception, messageId, resourceId);
			throw;
		}
	}
}

internal static partial class Log
{
	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Information,
		Message = "Processing message {MessageId} for resource {ResourceId}")]
	public static partial void ProcessingMessage(
		this ILogger logger,
		string messageId,
		string resourceId);

	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Warning,
		Message = "409 Conflict processing message {MessageId} for resource {ResourceId}")]
	public static partial void OnConflict(
		this ILogger logger,
		Exception exception,
		string messageId,
		string resourceId);

	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Error,
		Message = "Error processing message {MessageId} for resource {ResourceId}")]
	public static partial void OnException(
		this ILogger logger,
		Exception exception,
		string messageId,
		string resourceId);
}
