using System.ComponentModel.DataAnnotations;

namespace Defra.TradeImports.Messaging.Publishing;

/// <summary>A topic to publish to. Bindable from configuration.</summary>
public class TopicPublisherOptions
{
    [Required]
    public string TopicArn { get; set; } = null!;

    public int? MessageCompressionThresholdBytes { get; set; } = null;
}

/// <summary>
/// Publishes <typeparamref name="TMessage"/> to the one topic it was registered with, so callers
/// don't need to know the topic. Register one per topic with <c>AddPublisher</c>; give it a key to
/// have several topics for the same message type.
/// </summary>
public interface ITopicPublisher<in TMessage>
    where TMessage : IMessage
{
    string TopicArn { get; }

    Task PublishAsync(
        TMessage message,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        CancellationToken cancellationToken = default
    );
}

public class TopicPublisher<TMessage>(ISnsPublisher publisher, TopicPublisherOptions publisherOptions)
    : ITopicPublisher<TMessage>
    where TMessage : IMessage
{
    public string TopicArn { get; } = publisherOptions.TopicArn;

    public int? MessageCompressionThresholdBytes { get; } = publisherOptions.MessageCompressionThresholdBytes;

    public Task PublishAsync(
        TMessage message,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        CancellationToken cancellationToken = default
    )
    {
        return publisher.PublishAsync(TopicArn, message, headers, subject, MessageCompressionThresholdBytes, cancellationToken);
    }
}
