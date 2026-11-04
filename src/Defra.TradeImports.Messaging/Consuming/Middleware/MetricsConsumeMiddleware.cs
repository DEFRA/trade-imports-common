namespace Defra.TradeImports.Messaging.Consuming.Middleware;

public class MetricsConsumeMiddleware(
    ConsumerMetrics metrics,
    ILogger<MetricsConsumeMiddleware> logger
) : IConsumeMiddleware
{
    public async Task InvokeAsync(
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default
    )
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();
        var consumerName = context.ConsumerType.Name;

        try
        {
            metrics.Start(consumerName);
            await next();
        }
#pragma warning disable S2139
        catch (Exception exception)
#pragma warning restore S2139
        {
            metrics.Faulted(consumerName, exception);
            logger.LogError(exception, "Faulted consumer {Consumer}", consumerName);
            throw;
        }
        finally
        {
            metrics.Complete(
                consumerName,
                TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds
            );
        }
    }
}
