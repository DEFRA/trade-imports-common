using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Defra.TradeImports.Api.Metrics.Tests;

public abstract class TestServerBase : IDisposable
{
    private bool _disposed;
    private IHost? _host;

    protected HttpClient? TestClient;
    protected readonly IRequestMetrics RequestMetricsMock = Substitute.For<IRequestMetrics>();

    protected Action<IApplicationBuilder> SetupMiddleware = app =>
    {
        app.UseMiddleware<ApiMetricsMiddleware>();
        app.UseExceptionHandler(exceptionHandler =>
        {
            exceptionHandler.Run(async context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("Some unexpected fault happened.");
            });
        });
    };

    protected void SetupTestServer()
    {
        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureAppConfiguration(configurationBuilder =>
                    {
                        configurationBuilder.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["ApiMetrics:MeterName"] = "TestMeterName",
                                ["ApiMetrics:IgnoredPathPrefixes:0"] = "/ignored-path",
                                ["ApiMetrics:IgnoredPathPrefixes:1"] = "/ignored-template-path/{id}",
                            }
                        );
                    })
                    .ConfigureServices(services =>
                    {
                        services
                            .AddOptions<ApiMetricsOptions>()
                            .BindConfiguration("ApiMetrics")
                            .ValidateDataAnnotations()
                            .ValidateOnStart();
                        services.AddTransient<ApiMetricsMiddleware>();
                        services.AddSingleton(RequestMetricsMock);
                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        SetupMiddleware(app);

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/completing-path", () => Results.Ok());
                            endpoints.MapGet("/non-success-path", () => Results.NotFound());
                            endpoints.MapGet("/ignored-path", () => Results.Ok());
                            endpoints.MapGet("/faulting-path", string () => throw new Exception("Some exception"));
                            endpoints.MapGet("/template-path/{id}", () => Results.Ok());
                            endpoints.MapGet("/template-path/{id}/resource/{resourceId}", () => Results.Ok());
                            endpoints.MapGet("/ignored-template-path/{id}", () => Results.Ok());
                        });
                    });
            })
            .Start();

        TestClient = _host.GetTestClient();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            TestClient?.Dispose();
            _host?.Dispose();
        }
        _disposed = true;
    }

    ~TestServerBase()
    {
        Dispose(false);
    }
}
