using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.Api.Metrics;

public class ApiMetricsMiddleware(
    IOptions<ApiMetricsOptions> apiMetricsOptions,
    IRequestMetrics requestMetrics
) : IMiddleware
{
    private Exception? Exception { get; set; }
    
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        var path = context.Request.Path.HasValue ? context.Request.Path.Value : "unknown";

        if (IgnoreRequest(path))
        {
            await next(context);
            return;
        }

        context.Response.OnStarting(capturedState =>
        {
            var elapsed = TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds;
            var stateException = ((ApiMetricsMiddleware)capturedState).Exception;

            requestMetrics.RequestCompleted(
                path,
                context.Request.Method,
                context.Response.StatusCode,
                elapsed
            );

            if (stateException is not null || context.Response.StatusCode is < 200 or > 299)
            {
                requestMetrics.RequestFaulted(
                    path,
                    context.Request.Method,
                    context.Response.StatusCode,
                    stateException
                );
            }

            return Task.CompletedTask;
        }, this);

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            Exception = ex;
            throw;
        }
    }

    private bool IgnoreRequest(string path) =>
        apiMetricsOptions.Value.IgnoredPathPrefixes.Any(path.StartsWith);
}
