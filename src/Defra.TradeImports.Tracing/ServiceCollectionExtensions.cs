using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.Tracing;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddTraceContextAccessor(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ITraceContextAccessor, TraceContextAccessor>();
		services.AddOptions<TraceHeader>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Replaces use of AddHeaderPropagation so we can configure outside startup
		// and use the TraceHeader options configured above that will have been validated
		services.AddSingleton<IConfigureOptions<HeaderPropagationOptions>>(sp =>
		{
			var traceHeader = sp.GetRequiredService<IOptions<TraceHeader>>().Value;
			return new ConfigureOptions<HeaderPropagationOptions>(options =>
			{
				if (!string.IsNullOrWhiteSpace(traceHeader.Name))
					options.Headers.Add(traceHeader.Name);
			});
		});
		services.TryAddSingleton<HeaderPropagationValues>();

		return services;
	}
}