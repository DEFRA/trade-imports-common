using SlimMessageBus;

namespace Defra.TradeImports.SMB.Metrics;

public static class ConsumerContextExtensions
{
    public static string GetResourceType(this IConsumerContext consumerContext)
    {
        if (consumerContext.Headers.TryGetValue(MessageBusHeaders.ResourceType, out var value))
        {
            return value.ToString()!;
        }

        return string.Empty;
    }

    public static string GetSubResourceType(this IConsumerContext consumerContext)
    {
        if (consumerContext.Headers.TryGetValue(MessageBusHeaders.SubResourceType, out var value))
        {
            return value.ToString()!;
        }

        return string.Empty;
    }

    public static string GetResourceId(this IConsumerContext consumerContext)
    {
        if (consumerContext.Headers.TryGetValue(MessageBusHeaders.ResourceId, out var value))
        {
            return value.ToString()!;
        }

        return string.Empty;
    }
}
