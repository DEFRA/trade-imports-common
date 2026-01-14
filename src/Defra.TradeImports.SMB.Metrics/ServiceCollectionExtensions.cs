using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.Interceptor;

namespace Defra.TradeImports.SMB.Metrics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsumerMetrics(this IServiceCollection services)
    {
		services.AddSingleton<ConsumerMetrics>();
		services.AddSingleton(typeof(IConsumerInterceptor<>), typeof(MetricsInterceptor<>));

		return services;
    }
}
