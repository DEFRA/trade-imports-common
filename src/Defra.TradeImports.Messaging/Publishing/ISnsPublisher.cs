namespace Defra.TradeImports.Messaging.Publishing;

public interface ISnsPublisher
{
    Task PublishAsync(
        string topicArn,
        string messageBody,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        string? duplicationId = null,
        string? messageGroupId = null,
        CancellationToken cancellationToken = default
    );

    Task PublishAsync<T>(
        string topicArn,
        T message,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        CancellationToken cancellationToken = default
    )
        where T : IMessage;
}
