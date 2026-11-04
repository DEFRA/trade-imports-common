using Amazon.SimpleNotificationService.Model;
using Defra.TradeImports.Messaging.Publishing;

namespace Defra.TradeImports.Messaging.Extensions;

public static class PublishContextExtensions
{
    public static PublishRequest ToSnsPublishRequest(
        this PublishContext publishContext,
        string topicArn
    )
    {
        var request = new PublishRequest
        {
            TopicArn = topicArn,
            Message = publishContext.MessageBody,
            Subject = publishContext.Subject,
            MessageGroupId =
                publishContext.MessageGroupId
                ?? publishContext.Subject
                ?? Guid.CreateVersion7().ToString("N"),
            MessageAttributes = [],
        };

        foreach (var header in publishContext.Headers)
        {
            request.MessageAttributes[header.Key] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = header.Value,
            };
        }

        return request;
    }

    public static void SetTraceId(this PublishContext publishContext, string? traceId)
    {
        if (!string.IsNullOrEmpty(traceId))
            publishContext.Headers[MessagingHeaders.TraceId] = traceId;
    }

    public static string ToTopicNameFromTopicArn(this string topicArn)
    {
        var parts = topicArn.Split(':');
        return parts.Length < 6
            ? throw new FormatException("Invalid SNS Topic ARN format.")
            : parts[^1];
    }
}
