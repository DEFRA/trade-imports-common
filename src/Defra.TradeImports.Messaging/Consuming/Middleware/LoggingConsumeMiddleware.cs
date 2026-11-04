namespace Defra.TradeImports.Messaging.Consuming.Middleware;

public class LoggingConsumeMiddleware(ILogger<LoggingConsumeMiddleware> logger) : IConsumeMiddleware
{
    public async Task InvokeAsync(
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Consuming Message: {MessageId}", context.MessageId);
        try
        {
            await next();
        }
        finally
        {
            logger.LogInformation("Consumed Message: {MessageId}", context.MessageId);
        }
    }
}
