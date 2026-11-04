using System.Diagnostics.CodeAnalysis;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Defra.TradeImports.Messaging.Health;

[ExcludeFromCodeCoverage]
public class SqsHealthCheck(IAmazonSQS sqsClient, string queueUrl) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await sqsClient.GetQueueAttributesAsync(queueUrl, ["QueueArn"], cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                exception: new InvalidOperationException(
                    $"Failed to connect to AWS queue: {queueUrl}",
                    ex
                )
            );
        }
    }
}

[ExcludeFromCodeCoverage]
public class SnsHealthCheck(IAmazonSimpleNotificationService snsClient, string topicArn)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await snsClient.GetTopicAttributesAsync(
                new GetTopicAttributesRequest { TopicArn = topicArn },
                cancellationToken
            );
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                exception: new InvalidOperationException(
                    $"Failed to connect to AWS topic: {topicArn}",
                    ex
                )
            );
        }
    }
}

[ExcludeFromCodeCoverage]
public static class HealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddSqs(
        this IHealthChecksBuilder builder,
        string name,
        Func<IServiceProvider, string> queueUrl,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null
    ) =>
        builder.Add(
            new HealthCheckRegistration(
                name,
                sp => new SqsHealthCheck(sp.GetRequiredService<IAmazonSQS>(), queueUrl(sp)),
                HealthStatus.Unhealthy,
                tags,
                timeout
            )
        );

    public static IHealthChecksBuilder AddSns(
        this IHealthChecksBuilder builder,
        string name,
        Func<IServiceProvider, string> topicArn,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null
    ) =>
        builder.Add(
            new HealthCheckRegistration(
                name,
                sp => new SnsHealthCheck(
                    sp.GetRequiredService<IAmazonSimpleNotificationService>(),
                    topicArn(sp)
                ),
                HealthStatus.Unhealthy,
                tags,
                timeout
            )
        );
}
