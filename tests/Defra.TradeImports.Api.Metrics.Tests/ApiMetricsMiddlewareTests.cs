using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.TradeImports.Api.Metrics.Tests;

public class ApiMetricsMiddlewareTests
{
    private readonly IRequestMetrics _requestMetricsMock = Substitute.For<IRequestMetrics>();
    private readonly RequestDelegate _nextDelegateMock = Substitute.For<RequestDelegate>();
    
    private readonly DefaultHttpContext _context = new() { Request = { Method = "GET" } };
    private readonly ApiMetricsMiddleware _sut;

    public ApiMetricsMiddlewareTests()
    {
        var settings = new ApiMetricsOptions {
            MeterName = "ApiMetricsMiddlewareTests",
            IgnoredPathPrefixes = [ "/some-ignored-path" ]
        };
        var options = Options.Create(settings);
        
        _sut = new ApiMetricsMiddleware(options, _requestMetricsMock);
    }
    
    [Fact]
    public async Task Should_Record_Completed_Requests()
    {
        var routedEndpoint = new RouteEndpoint(
            async c => await c.Response.WriteAsync("Test"),
            RoutePatternFactory.Parse("/some-path"),
            0,
            null,
            null
        );
        _context.SetEndpoint(routedEndpoint);
        
        await _sut.InvokeAsync(_context, _nextDelegateMock);
        
        _context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _requestMetricsMock.Received(1).RequestCompleted(
            Arg.Is<string>("/some-path"),
            Arg.Is<string>("GET"),
            Arg.Is(200),
            Arg.Any<double>());
    }
    
    [Fact]
    public async Task Should_Record_Completed_Requests_To_Any_Unknown_Paths()
    {
        await _sut.InvokeAsync(_context, _nextDelegateMock);
        
        _context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _requestMetricsMock.Received(1).RequestCompleted(
            Arg.Is<string>("unknown"),
            Arg.Is<string>("GET"),
            Arg.Is(200),
            Arg.Any<double>());
    }
    
    [Fact]
    public async Task Should_Not_Record_Ignored_Requests()
    {
        var routedEndpoint = new RouteEndpoint(
            async c => await c.Response.WriteAsync("Test"),
            RoutePatternFactory.Parse("/some-ignored-path"),
            0,
            null,
            null
        );
        _context.SetEndpoint(routedEndpoint);
        
        await _sut.InvokeAsync(_context, _nextDelegateMock);
        
        _context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _requestMetricsMock.DidNotReceive().RequestCompleted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<double>());
    }
    
    [Fact]
    public async Task Should_Record_Faulted_Requests()
    {
        _context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var routedEndpoint = new RouteEndpoint(
            async c => await c.Response.WriteAsync("Test"),
            RoutePatternFactory.Parse("/some-path"),
            0,
            null,
            null
        );
        _context.SetEndpoint(routedEndpoint);
        _nextDelegateMock.When(d => d.Invoke(Arg.Any<HttpContext>())).Throw(new Exception("Test exception"));
        
        await Assert.ThrowsAsync<Exception>(() => _sut.InvokeAsync(_context, _nextDelegateMock));
        
        _context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        _requestMetricsMock.Received(1).RequestFaulted(
            Arg.Is<string>("/some-path"),
            Arg.Is<string>("GET"),
            Arg.Is(500),
            Arg.Any<Exception>());
        _requestMetricsMock.Received(1).RequestCompleted(
            Arg.Is<string>("/some-path"),
            Arg.Is<string>("GET"),
            Arg.Is(500),
            Arg.Any<double>());
    }
}