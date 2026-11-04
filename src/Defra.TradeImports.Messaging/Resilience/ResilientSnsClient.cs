using System.Diagnostics.CodeAnalysis;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Defra.TradeImports.Messaging.Resilience;

/// <summary>
/// SNS client whose batch publish retries transient AWS errors and transiently failed entries (from
/// trade-gateway-publisher). Single publishes are unchanged: their failures surface to the caller,
/// which for a consumer means SQS redelivery.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ResilientSnsClient : AmazonSimpleNotificationServiceClient
{
    private readonly ResilientSnsClientRetryHandler _retryHandler;

    public ResilientSnsClient(ILogger<ResilientSnsClient> logger)
    {
        _retryHandler = new ResilientSnsClientRetryHandler(logger);
    }

    public ResilientSnsClient(
        ILogger<ResilientSnsClient> logger,
        AWSCredentials credentials,
        AmazonSimpleNotificationServiceConfig config
    )
        : base(credentials, config)
    {
        _retryHandler = new ResilientSnsClientRetryHandler(logger);
    }

    public override Task<PublishBatchResponse> PublishBatchAsync(
        PublishBatchRequest request,
        CancellationToken cancellationToken = default
    ) => _retryHandler.PublishWithRetryAsync(base.PublishBatchAsync, request, cancellationToken);
}
