using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Defra.TradeImports.SQS.Endpoints;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddDeadLetterQueueManagementServices(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISqsDeadLetterService, SqsDeadLetterService>();

		return services;
	}
}