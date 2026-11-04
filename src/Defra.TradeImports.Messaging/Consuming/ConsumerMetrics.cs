using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Defra.TradeImports.Messaging.Consuming;

[ExcludeFromCodeCoverage]
public class ConsumerMetrics
{
    private readonly Histogram<double> _consumeDuration;
    private readonly Counter<long> _consumeTotal;
    private readonly Counter<long> _consumeFaultTotal;
    private readonly Counter<long> _consumerInProgress;

    public ConsumerMetrics(IMeterFactory meterFactory, string meterName)
    {
        var meter = meterFactory.Create(meterName);

        _consumeTotal = meter.CreateCounter<long>(
            "MessagingConsume",
            "COUNT",
            "Number of messages consumed"
        );
        _consumeFaultTotal = meter.CreateCounter<long>(
            "MessagingConsumeErrors",
            "COUNT",
            "Number of message consume faults"
        );
        _consumerInProgress = meter.CreateCounter<long>(
            "MessagingConsumeActive",
            "COUNT",
            "Number of consumers in progress"
        );
        _consumeDuration = meter.CreateHistogram<double>(
            "MessagingConsumeDuration",
            "MILLISECONDS",
            "Elapsed time spent consuming a message, in millis"
        );
    }

    public void Start(string consumerName)
    {
        var tagList = BuildTags(consumerName);
        _consumeTotal.Add(1, tagList);
        _consumerInProgress.Add(1, tagList);
    }

    public void Faulted(string consumerName, Exception exception)
    {
        var tagList = BuildTags(consumerName);
        tagList.Add("ExceptionType", exception.GetType().Name);
        _consumeFaultTotal.Add(1, tagList);
    }

    public void Complete(string consumerName, double milliseconds)
    {
        var tagList = BuildTags(consumerName);
        _consumerInProgress.Add(-1, tagList);
        _consumeDuration.Record(milliseconds, tagList);
    }

    private static TagList BuildTags(string consumerName) =>
        new()
        {
            { "ServiceName", Process.GetCurrentProcess().ProcessName },
            { "ConsumerType", consumerName },
        };
}
