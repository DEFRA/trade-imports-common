using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.Interceptor;

namespace Defra.TradeImports.SMB.Tracing;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddTraceContextInterceptor(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddSingleton(typeof(IConsumerInterceptor<>), typeof(TraceContextInterceptor<>));

		return services;
	}
}