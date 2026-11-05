using Amazon.SimpleNotificationService;

using Defra.TradeImports.Messaging.Encoding;
using Defra.TradeImports.Messaging.Extensions;

using System.Text.Json;

namespace Defra.TradeImports.Messaging.Publishing;

/// <summary>Mirrors trade-gateway-publisher's <c>SnsPublisher</c>: a publish wrapped in a middleware pipeline.</summary>
public class SnsPublisher(
    IAmazonSimpleNotificationService snsClient,
    IMessageEncoder messageEncoder,
    IEnumerable<IPublishMiddleware>? middlewares = null
) : ISnsPublisher
{
    private readonly IReadOnlyList<IPublishMiddleware> _middlewares = middlewares?.ToList() ?? [];

    public async Task PublishAsync(
        string topicArn,
        string messageBody,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        string? duplicationId = null,
        string? messageGroupId = null,
        int? compressionThresholdBytes = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(topicArn))
            throw new ArgumentException("Topic ARN is required.", nameof(topicArn));

        if (string.IsNullOrWhiteSpace(messageBody))
            throw new ArgumentException("Message body is required.", nameof(messageBody));

        // compress if required
        if (headers?.ContainsKey(MessageHeaders.ContentEncoding) != true && compressionThresholdBytes > 0)
        {
            var encoded = messageEncoder.Encode(messageBody, compressionThresholdBytes.Value);
            messageBody = encoded.content;

            if (!string.IsNullOrWhiteSpace(encoded.contentEncoding))
            {
                headers ??= [];
                headers.Add(MessageHeaders.ContentEncoding, encoded.contentEncoding);
            }
        }

        var context = new PublishContext
        {
            TopicName = topicArn.ToTopicNameFromTopicArn(),
            MessageBody = messageBody,
            Subject = subject,
            MessageGroupId = messageGroupId,
            Headers = headers ?? new Dictionary<string, string>(),
        };

        Task PublishCore()
        {
            var request = context.ToSnsPublishRequest(topicArn);
            request.MessageDeduplicationId = duplicationId;
            return snsClient.PublishAsync(request, cancellationToken);
        }

        var pipeline = PublishCore;

        foreach (var middleware in _middlewares.Reverse())
        {
            var next = pipeline;
            pipeline = () => middleware.InvokeAsync(context, next, cancellationToken);
        }

        await pipeline();
    }

    public Task PublishAsync<T>(
        string topicArn,
        T message,
        Dictionary<string, string>? headers = null,
        string? subject = null,
        int? compressionThresholdBytes = null,
        CancellationToken cancellationToken = default
    )
        where T : IMessage
    {
        if (string.IsNullOrWhiteSpace(topicArn))
            throw new ArgumentException("Topic ARN is required.", nameof(topicArn));

        if (message is null)
            throw new ArgumentException("Message is required.", nameof(message));

        return PublishAsync(
            topicArn,
            JsonSerializer.Serialize(message),
            headers,
            subject,
            message.DuplicationId,
            message.MessageGroupId,
            compressionThresholdBytes,
            cancellationToken
        );
    }
}
