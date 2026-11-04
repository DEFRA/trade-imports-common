namespace Defra.TradeImports.Messaging.Publishing;

public class PublishContext
{
    public string TopicName { get; init; } = default!;

    public string MessageBody { get; init; } = default!;

    public string? Subject { get; init; }

    public string? MessageGroupId { get; init; }

    public Dictionary<string, string> Headers { get; init; } = new();
}
