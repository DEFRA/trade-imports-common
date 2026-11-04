namespace Defra.TradeImports.Messaging.Consuming;

public interface IMessageConsumer
{
    Task ConsumeAsync(MessageContext context, CancellationToken cancellationToken = default);
}
