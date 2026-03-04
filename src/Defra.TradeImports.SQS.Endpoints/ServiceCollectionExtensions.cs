using Microsoft.Extensions.DependencyInjection;

namespace Defra.TradeImports.SQS.Endpoints;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddDeadLetterQueueManagementServices(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddSingleton<ISqsDeadLetterService, SqsDeadLetterService>();

		return services;
	}
}