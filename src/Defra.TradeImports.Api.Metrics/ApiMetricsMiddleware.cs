using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.Api.Metrics;

public class ApiMetricsMiddleware(IOptions<ApiMetricsOptions> apiMetricsOptions, IRequestMetrics requestMetrics) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        var path = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unknown";

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            requestMetrics.RequestFaulted(path, context.Request.Method, context.Response.StatusCode, ex);
        }
        finally
        {
            if (!IgnoreRequest(path))
            {
                requestMetrics.RequestCompleted(
                    path,
                    context.Request.Method,
                    context.Response.StatusCode,
                    TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds
                );
            }
        }
    }

    private bool IgnoreRequest(string path) => apiMetricsOptions.Value.IgnoredPathPrefixes.Any(path.StartsWith);
}