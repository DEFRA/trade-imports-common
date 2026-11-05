using Amazon.SQS.Model;

namespace Defra.TradeImports.Messaging.Consuming;

public class MessageContext
{
    public required Message Message { get; init; }

    public required string QueueUrl { get; init; }

    public required Type ConsumerType { get; init; }

    public required string Body { get; init; }

    public string MessageId => Message.MessageId;

    public IReadOnlyDictionary<string, MessageAttributeValue> Headers =>
        (Message.MessageAttributes ?? new Dictionary<string, MessageAttributeValue>()).AsReadOnly();

    public string? GetHeader(string key) =>
        Headers.TryGetValue(key, out var value) ? value.StringValue : null;

    public string GetTraceId() =>
        GetHeader(MessagingHeaders.TraceId) ?? Guid.NewGuid().ToString("N");
}
