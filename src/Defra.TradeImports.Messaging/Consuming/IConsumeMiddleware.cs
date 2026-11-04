namespace Defra.TradeImports.Messaging.Consuming;

public interface IConsumeMiddleware
{
    Task InvokeAsync(
        MessageContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default
    );
}
