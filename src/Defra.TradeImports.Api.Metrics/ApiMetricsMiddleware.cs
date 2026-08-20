using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.Api.Metrics;

public class ApiMetricsMiddleware(
    IOptions<ApiMetricsOptions> apiMetricsOptions,
    IRequestMetrics requestMetrics
) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        var path = context.Request.Path.HasValue ? context.Request.Path.Value : "unknown";
        var exceptionFaultRecorded = false;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            exceptionFaultRecorded = true;
            requestMetrics.RequestFaulted(
                path,
                context.Request.Method,
                context.Response.StatusCode,
                ex
            );
            throw;
        }
        finally
        {
            if (!IgnoreRequest(path) && !exceptionFaultRecorded)
            {
                if (context.Response.StatusCode is >= 200 and < 300)
                {
                    requestMetrics.RequestCompleted(
                        path,
                        context.Request.Method,
                        context.Response.StatusCode,
                        TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds
                    );
                }
                else
                {
                    requestMetrics.RequestFaulted(
                        path,
                        context.Request.Method,
                        context.Response.StatusCode
                    );
                }
            }
        }
    }

    private bool IgnoreRequest(string path) =>
        apiMetricsOptions.Value.IgnoredPathPrefixes.Any(path.StartsWith);
}
