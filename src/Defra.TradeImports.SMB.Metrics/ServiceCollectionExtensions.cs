using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.Interceptor;

namespace Defra.TradeImports.SMB.Metrics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsumerMetrics(this IServiceCollection services, string meterName)
    {
		services.AddSingleton<ConsumerMetrics>(sp => new ConsumerMetrics(sp.GetRequiredService<IMeterFactory>(), meterName));
		services.AddSingleton(typeof(IConsumerInterceptor<>), typeof(MetricsInterceptor<>));

		return services;
    }
}
