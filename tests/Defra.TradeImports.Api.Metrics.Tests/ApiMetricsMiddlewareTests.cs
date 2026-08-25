using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Defra.TradeImports.Api.Metrics.Tests;

public class ApiMetricsMiddlewareTests : TestServerBase
{
    [Fact]
    public async Task Should_Record_Completed_Requests()
    {
        SetupTestServer();

        var response = await Assert.IsType<HttpClient>(TestClient).GetAsync("/completing-path");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        RequestMetricsMock
            .Received(1)
            .RequestCompleted(
                Arg.Is<string>("/completing-path"),
                Arg.Is<string>("GET"),
                Arg.Is(200),
                Arg.Any<double>()
            );
        RequestMetricsMock
            .DidNotReceive()
            .RequestFaulted(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<Exception>()
            );
    }

    [Fact]
    public async Task Should_Not_Record_Ignored_Requests()
    {
        SetupTestServer();

        var response = await Assert.IsType<HttpClient>(TestClient).GetAsync("/ignored-path");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        RequestMetricsMock.DidNotReceive().RequestCompleted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<double>());
        RequestMetricsMock.DidNotReceive().RequestFaulted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Exception>());
    }

    [Fact]
    public async Task When_Response_Is_Non_Successful_Should_Record_Faulted_Requests()
    {
        SetupTestServer();

        var response = await Assert.IsType<HttpClient>(TestClient).GetAsync("/non-success-path");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        RequestMetricsMock.Received(1).RequestCompleted(
            Arg.Is<string>("/non-success-path"),
            Arg.Is<string>("GET"),
            Arg.Is(404),
            Arg.Any<double>());
        RequestMetricsMock.Received(1).RequestFaulted(
            Arg.Is<string>("/non-success-path"),
            Arg.Is<string>("GET"),
            Arg.Is(404));
    }

    [Fact]
    public async Task When_Exception_Occurs_With_Exception_Handling_Registered_Should_Record_Faulted_Requests()
    {
        SetupTestServer();

        await Assert.IsType<HttpClient>(TestClient).GetAsync("/faulting-path");

        RequestMetricsMock.Received(1).RequestFaulted(
            Arg.Is<string>("/faulting-path"),
            Arg.Is<string>("GET"),
            Arg.Is(500));
    }

    [Fact]
    public async Task When_Exception_Occurs_With_Exception_Handling_Registered_First_Should_Record_Faulted_Requests()
    {
        SetupMiddleware = app =>
        {
            app.UseExceptionHandler(exceptionHandler =>
            {
                exceptionHandler.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    await context.Response.WriteAsync("Some unexpected fault happened.");
                });
            });
            app.UseMiddleware<ApiMetricsMiddleware>();
        };
        SetupTestServer();

        await Assert.IsType<HttpClient>(TestClient).GetAsync("/faulting-path");

        RequestMetricsMock.Received(1).RequestFaulted(
            Arg.Is<string>("/faulting-path"),
            Arg.Is<string>("GET"),
            Arg.Is(500),
            Arg.Any<Exception>());
    }

    [Fact]
    public async Task When_Exception_Occurs_During_Multiple_Parallel_Requests_Should_Record_Faulted_Requests()
    {
        SetupMiddleware = app =>
        {
            app.UseExceptionHandler(exceptionHandler =>
            {
                exceptionHandler.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    await context.Response.WriteAsync("Some unexpected fault happened.");
                });
            });
            app.UseMiddleware<ApiMetricsMiddleware>();
        };
        SetupTestServer();

        await Task.WhenAll(
            Assert.IsType<HttpClient>(TestClient).GetAsync("/faulting-path"),
            Assert.IsType<HttpClient>(TestClient).GetAsync("/completing-path"),
            Assert.IsType<HttpClient>(TestClient).GetAsync("/completing-path")
        );

        RequestMetricsMock.Received(1).RequestFaulted(
            Arg.Is<string>("/faulting-path"),
            Arg.Is<string>("GET"),
            Arg.Is(500),
            Arg.Any<Exception>());
    }

    [Fact]
    public async Task When_Exception_Occurs_With_No_Exception_Handling_Registered_Should_Rethrow_Exception()
    {
        SetupMiddleware = app =>
        {
            app.UseMiddleware<ApiMetricsMiddleware>();
        };
        SetupTestServer();

        await Assert.ThrowsAsync<Exception>(() => Assert.IsType<HttpClient>(TestClient).GetAsync("/faulting-path"));

        RequestMetricsMock.DidNotReceive().RequestCompleted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<double>());
        RequestMetricsMock.DidNotReceive().RequestFaulted(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Exception>());
    }
}
