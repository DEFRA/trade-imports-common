using System.Diagnostics;
using System.Diagnostics.Metrics;
using Amazon.CloudWatch.EMF.Model;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.Api.Metrics;

public interface IRequestMetrics
{
    void RequestCompleted(string requestPath, string httpMethod, int statusCode, double milliseconds);
    void RequestFaulted(string requestPath, string httpMethod, int statusCode, Exception? exception = null);
}

public class RequestMetrics : IRequestMetrics
{
    private readonly Counter<long> _requestsReceived;
    private readonly Counter<long> _requestsFaulted;
    private readonly Histogram<double> _requestDuration;

    public RequestMetrics(IMeterFactory meterFactory, IOptions<ApiMetricsOptions> apiMetricsOptions)
    {
        var meter = meterFactory.Create(apiMetricsOptions.Value.MeterName);

        _requestsReceived = meter.CreateCounter<long>(
            MetricsConstants.InstrumentNames.RequestReceived,
            nameof(Unit.COUNT),
            "Count of requests received"
        );

        _requestsFaulted = meter.CreateCounter<long>(
            MetricsConstants.InstrumentNames.RequestFaulted,
            nameof(Unit.COUNT),
            "Count of request faults"
        );
        
        _requestDuration = meter.CreateHistogram<double>(
            MetricsConstants.InstrumentNames.RequestDuration,
            nameof(Unit.MILLISECONDS),
            "Duration of request"
        );
    }
    
    public void RequestCompleted(string requestPath, string httpMethod, int statusCode, double milliseconds)
    {
        _requestsReceived.Add(1, BuildTags(requestPath, httpMethod, statusCode));
        _requestDuration.Record(milliseconds, BuildTags(requestPath, httpMethod, statusCode));
    }

    public void RequestFaulted(string requestPath, string httpMethod, int statusCode, Exception? exception = null)
    {
        var tagList = BuildTags(requestPath, httpMethod, statusCode);
        
        if (exception is not null)
        {
            tagList.Add(MetricsConstants.RequestTags.ExceptionType, exception.GetType().Name);
        }

        _requestsFaulted.Add(1, tagList);
    }

    private static TagList BuildTags(string requestPath, string httpMethod, int statusCode)
    {
        return new TagList
        {
            { MetricsConstants.RequestTags.Service, Process.GetCurrentProcess().ProcessName },
            { MetricsConstants.RequestTags.RequestPath, requestPath },
            { MetricsConstants.RequestTags.HttpMethod, httpMethod },
            { MetricsConstants.RequestTags.StatusCode, statusCode },
        };
    }
}