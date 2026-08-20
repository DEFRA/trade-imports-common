using FluentAssertions;
using Microsoft.AspNetCore.Http;
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
        _context.Request.Path =  new PathString("/some-path");
        
        await _sut.InvokeAsync(_context, _nextDelegateMock);
        
        _context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _requestMetricsMock.Received(1).RequestCompleted(
            Arg.Is<string>("/some-path"),
            Arg.Is<string>("GET"),
            Arg.Is(200),
            Arg.Any<double>());
        _requestMetricsMock.DidNotReceive().RequestFaulted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Exception>());
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
        _context.Request.Path =  new PathString("/some-ignored-path");
        
        await _sut.InvokeAsync(_context, _nextDelegateMock);
        
        _context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _requestMetricsMock.DidNotReceive().RequestCompleted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<double>());
    }
    
    // Most exceptions should be handled by Exception Handling middleware. If an exception isn't handled, or there isn't
    // any exception handling middleware, status code is not set, hence the default OK status code used in this test
    [Fact]
    public async Task When_Exception_Occurs_Should_Record_Faulted_Requests()
    {
        _context.Response.StatusCode = StatusCodes.Status200OK;
        _context.Request.Path =  new PathString("/some-path");
        var thrownException = new Exception("Test exception");
        _nextDelegateMock.When(d => d.Invoke(Arg.Any<HttpContext>())).Throw(thrownException);
        
        await Assert.ThrowsAsync<Exception>(() => _sut.InvokeAsync(_context, _nextDelegateMock));
        
        _context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _requestMetricsMock.Received(1).RequestFaulted(
            Arg.Is<string>("/some-path"),
            Arg.Is<string>("GET"),
            Arg.Is(200),
            Arg.Is(thrownException));
        _requestMetricsMock.DidNotReceive().RequestCompleted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<double>());
    }
    
    // In this case, the status code should have been set by the Exception Handler middleware, therefore there won't
    // be an exception
    [Fact]
    public async Task When_Response_Is_Not_Successful_Should_Record_Faulted_Requests()
    {
        _context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        _context.Request.Path =  new PathString("/some-path");
        
        await _sut.InvokeAsync(_context, _nextDelegateMock);
        
        _context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        _requestMetricsMock.DidNotReceive().RequestCompleted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<double>());
        _requestMetricsMock.Received(1).RequestFaulted(
            Arg.Is<string>("/some-path"),
            Arg.Is<string>("GET"),
            Arg.Is(500));
    }
}
