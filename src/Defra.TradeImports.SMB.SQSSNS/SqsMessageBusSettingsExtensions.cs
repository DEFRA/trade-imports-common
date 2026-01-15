using Amazon;
using Microsoft.Extensions.Configuration;
using SlimMessageBus.Host.AmazonSQS;

namespace Defra.TradeImports.SMB.SQSSNS;

public static class SqsMessageBusSettingsExtensions
{
	private const string DefaultRegion = "eu-west-2";

	public static SqsMessageBusSettings UseLocalOrAmbientCredentials(this SqsMessageBusSettings settings, IConfiguration configuration)
	{
		var clientId = configuration.GetValue<string>("AWS_ACCESS_KEY_ID");
		var clientSecret = configuration.GetValue<string>("AWS_SECRET_ACCESS_KEY");

		if (!string.IsNullOrEmpty(clientSecret) && !string.IsNullOrEmpty(clientId))
		{
			var region = configuration.GetValue<string>("AWS_REGION") ?? DefaultRegion;
			var regionEndpoint = RegionEndpoint.GetBySystemName(region);
			settings.SnsClientConfig.AuthenticationRegion = region;
			settings.SnsClientConfig.RegionEndpoint = regionEndpoint;
			settings.SnsClientConfig.ServiceURL = configuration.GetValue<string>("SQS_Endpoint");
			settings.SqsClientConfig.AuthenticationRegion = region;
			settings.SqsClientConfig.RegionEndpoint = regionEndpoint;
			settings.SqsClientConfig.ServiceURL = configuration.GetValue<string>("SQS_Endpoint");
			settings.UseStaticCredentials(clientId, clientSecret);
		}
		else
		{
			settings.UseAmbientCredentials();
		}

		return settings;
	}
}