using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.Api.Metrics.Tests;

public class RequestMetricsTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMeterFactory _meterFactory;

    public RequestMetricsTests()
    {
        _serviceProvider = CreateServiceProvider();
        _meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var settings = new ApiMetricsOptions
        {
            MeterName = "RequestMetricsTests",
            IgnoredPathPrefixes = ["/some-ignored-path"],
        };
        var options = Options.Create(settings);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddMetrics();
        serviceCollection.AddSingleton(options);
        serviceCollection.AddSingleton<IRequestMetrics, RequestMetrics>();
        return serviceCollection.BuildServiceProvider();
    }

    private MetricCollector<T> GetCollector<T>(string instrumentName)
        where T : struct
    {
        return new MetricCollector<T>(_meterFactory, nameof(RequestMetricsTests), instrumentName);
    }

    [Fact]
    public void RecordRequestCompleted_ShouldContainMeasurement()
    {
        var metricsService = _serviceProvider.GetRequiredService<IRequestMetrics>();
        var requestReceivedCollector = GetCollector<long>(MetricsConstants.InstrumentNames.RequestReceived);
        var requestDurationCollector = GetCollector<double>(MetricsConstants.InstrumentNames.RequestDuration);

        metricsService.RequestCompleted("/some-url-path", "GET", 200, 123);
        metricsService.RequestCompleted("/some-other-url-path", "POST", 202, 456);

        var receivedMeasurements = requestReceivedCollector.GetMeasurementSnapshot();
        receivedMeasurements.Count.Should().Be(2);
        receivedMeasurements[0].Value.Should().Be(1);
        receivedMeasurements[0].ContainsTags(MetricsConstants.RequestTags.RequestPath).Should().BeTrue();
        receivedMeasurements[0].Tags[MetricsConstants.RequestTags.RequestPath].Should().Be("/some-url-path");
        receivedMeasurements[0].ContainsTags(MetricsConstants.RequestTags.HttpMethod).Should().BeTrue();
        receivedMeasurements[0].Tags[MetricsConstants.RequestTags.HttpMethod].Should().Be("GET");
        receivedMeasurements[0].ContainsTags(MetricsConstants.RequestTags.StatusCode).Should().BeTrue();
        receivedMeasurements[0].Tags[MetricsConstants.RequestTags.StatusCode].Should().Be(200);

        receivedMeasurements[1].Value.Should().Be(1);
        receivedMeasurements[1].ContainsTags(MetricsConstants.RequestTags.RequestPath).Should().BeTrue();
        receivedMeasurements[1].Tags[MetricsConstants.RequestTags.RequestPath].Should().Be("/some-other-url-path");
        receivedMeasurements[1].ContainsTags(MetricsConstants.RequestTags.HttpMethod).Should().BeTrue();
        receivedMeasurements[1].Tags[MetricsConstants.RequestTags.HttpMethod].Should().Be("POST");
        receivedMeasurements[1].ContainsTags(MetricsConstants.RequestTags.StatusCode).Should().BeTrue();
        receivedMeasurements[1].Tags[MetricsConstants.RequestTags.StatusCode].Should().Be(202);

        var durationMeasurements = requestDurationCollector.GetMeasurementSnapshot();
        durationMeasurements.Count.Should().Be(2);
        durationMeasurements[0].Value.Should().Be(123);
        durationMeasurements[0].ContainsTags(MetricsConstants.RequestTags.RequestPath).Should().BeTrue();
        durationMeasurements[0].Tags[MetricsConstants.RequestTags.RequestPath].Should().Be("/some-url-path");
        durationMeasurements[0].ContainsTags(MetricsConstants.RequestTags.HttpMethod).Should().BeTrue();
        durationMeasurements[0].Tags[MetricsConstants.RequestTags.HttpMethod].Should().Be("GET");
        durationMeasurements[0].ContainsTags(MetricsConstants.RequestTags.StatusCode).Should().BeTrue();
        durationMeasurements[0].Tags[MetricsConstants.RequestTags.StatusCode].Should().Be(200);

        durationMeasurements[1].Value.Should().Be(456);
        durationMeasurements[1].ContainsTags(MetricsConstants.RequestTags.RequestPath).Should().BeTrue();
        durationMeasurements[1].Tags[MetricsConstants.RequestTags.RequestPath].Should().Be("/some-other-url-path");
        durationMeasurements[1].ContainsTags(MetricsConstants.RequestTags.HttpMethod).Should().BeTrue();
        durationMeasurements[1].Tags[MetricsConstants.RequestTags.HttpMethod].Should().Be("POST");
        durationMeasurements[1].ContainsTags(MetricsConstants.RequestTags.StatusCode).Should().BeTrue();
        durationMeasurements[1].Tags[MetricsConstants.RequestTags.StatusCode].Should().Be(202);
    }

    [Fact]
    public void RecordRequestFaulted_ShouldContainMeasurement()
    {
        var metricsService = _serviceProvider.GetRequiredService<IRequestMetrics>();
        var requestFaultedCollector = GetCollector<long>(MetricsConstants.InstrumentNames.RequestFaulted);

        metricsService.RequestFaulted("/some-url-path", "GET", 500, new Exception("Some error 1"));
        metricsService.RequestFaulted("/some-other-url-path", "POST", 501, new Exception("Some error 2"));
        metricsService.RequestFaulted("/exception-handled-path", "PUT", 502);

        var faultedMeasurements = requestFaultedCollector.GetMeasurementSnapshot();
        faultedMeasurements.Count.Should().Be(3);
        faultedMeasurements[0].Value.Should().Be(1);
        faultedMeasurements[0].ContainsTags(MetricsConstants.RequestTags.RequestPath).Should().BeTrue();
        faultedMeasurements[0].Tags[MetricsConstants.RequestTags.RequestPath].Should().Be("/some-url-path");
        faultedMeasurements[0].ContainsTags(MetricsConstants.RequestTags.HttpMethod).Should().BeTrue();
        faultedMeasurements[0].Tags[MetricsConstants.RequestTags.HttpMethod].Should().Be("GET");
        faultedMeasurements[0].ContainsTags(MetricsConstants.RequestTags.StatusCode).Should().BeTrue();
        faultedMeasurements[0].Tags[MetricsConstants.RequestTags.StatusCode].Should().Be(500);
        faultedMeasurements[0].ContainsTags(MetricsConstants.RequestTags.ExceptionType).Should().BeTrue();
        faultedMeasurements[0].Tags[MetricsConstants.RequestTags.ExceptionType].Should().Be("Exception");

        faultedMeasurements[1].Value.Should().Be(1);
        faultedMeasurements[1].ContainsTags(MetricsConstants.RequestTags.RequestPath).Should().BeTrue();
        faultedMeasurements[1].Tags[MetricsConstants.RequestTags.RequestPath].Should().Be("/some-other-url-path");
        faultedMeasurements[1].ContainsTags(MetricsConstants.RequestTags.HttpMethod).Should().BeTrue();
        faultedMeasurements[1].Tags[MetricsConstants.RequestTags.HttpMethod].Should().Be("POST");
        faultedMeasurements[1].ContainsTags(MetricsConstants.RequestTags.StatusCode).Should().BeTrue();
        faultedMeasurements[1].Tags[MetricsConstants.RequestTags.StatusCode].Should().Be(501);
        faultedMeasurements[1].ContainsTags(MetricsConstants.RequestTags.ExceptionType).Should().BeTrue();
        faultedMeasurements[1].Tags[MetricsConstants.RequestTags.ExceptionType].Should().Be("Exception");

        faultedMeasurements[2].Value.Should().Be(1);
        faultedMeasurements[2].ContainsTags(MetricsConstants.RequestTags.RequestPath).Should().BeTrue();
        faultedMeasurements[2].Tags[MetricsConstants.RequestTags.RequestPath].Should().Be("/exception-handled-path");
        faultedMeasurements[2].ContainsTags(MetricsConstants.RequestTags.HttpMethod).Should().BeTrue();
        faultedMeasurements[2].Tags[MetricsConstants.RequestTags.HttpMethod].Should().Be("PUT");
        faultedMeasurements[2].ContainsTags(MetricsConstants.RequestTags.StatusCode).Should().BeTrue();
        faultedMeasurements[2].Tags[MetricsConstants.RequestTags.StatusCode].Should().Be(502);
        faultedMeasurements[2].ContainsTags(MetricsConstants.RequestTags.ExceptionType).Should().BeFalse();
    }
}
