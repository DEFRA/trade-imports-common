namespace Defra.TradeImports.Messaging.Publishing;

public interface IPublishMiddleware
{
    Task InvokeAsync(
        PublishContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default
    );
}
