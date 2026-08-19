using Microsoft.Extensions.DependencyInjection;

namespace Defra.TradeImports.Api.Metrics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiMetrics(this IServiceCollection services)
    {
        services.AddOptions<ApiMetricsOptions>().BindConfiguration("ApiMetrics").ValidateDataAnnotations().ValidateOnStart();
        services.AddTransient<ApiMetricsMiddleware>();
        services.AddSingleton<IRequestMetrics, RequestMetrics>();

        return services;
    }
}