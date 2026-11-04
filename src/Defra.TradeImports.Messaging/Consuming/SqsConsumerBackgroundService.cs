using Amazon.SQS;
using Amazon.SQS.Model;

namespace Defra.TradeImports.Messaging.Consuming;

/// <summary>
/// Long-polls an SQS queue and runs each message through the consume middleware pipeline. A message
/// is deleted only when its consumer succeeds; otherwise it becomes visible again after the
/// visibility timeout and is retried until the queue's redrive policy moves it to the dead-letter
/// queue. The consumer is resolved from a new DI scope for each message, so it may use scoped services.
/// </summary>
public class SqsConsumerBackgroundService<TConsumer>(
    SqsConsumerOptions options,
    IAmazonSQS sqsClient,
    IServiceScopeFactory scopeFactory,
    ILogger<SqsConsumerBackgroundService<TConsumer>> logger,
    IEnumerable<IConsumeMiddleware>? middlewares = null
) : BackgroundService
    where TConsumer : IMessageConsumer
{
    private readonly IReadOnlyList<IConsumeMiddleware> _middlewares = middlewares?.ToList() ?? [];

    public string QueueUrl => options.QueueUrl;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("SQS consumer for queue {QueueUrl} is disabled", QueueUrl);
            return;
        }

        logger.LogInformation("Starting SQS consumer for queue {QueueUrl}", QueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = QueueUrl,
                        MaxNumberOfMessages = options.MaxMessages,
                        WaitTimeSeconds = options.WaitTimeSeconds,
                        VisibilityTimeout = options.VisibilityTimeoutSeconds,
                        MessageAttributeNames = ["All"],
                    },
                    stoppingToken
                );

                if (response.Messages == null || response.Messages.Count == 0)
                    continue;

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }
            }
            catch (QueueDoesNotExistException ex)
            {
                logger.LogError(ex, "Queue does not exist {QueueUrl}", QueueUrl);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while polling SQS {QueueUrl}", QueueUrl);
            }
        }

        logger.LogInformation("Stopping SQS consumer for queue {QueueUrl}", QueueUrl);
    }

    internal async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var consumer = scope.ServiceProvider.GetRequiredService<TConsumer>();

        var context = new MessageContext
        {
            Message = message,
            QueueUrl = QueueUrl,
            ConsumerType = typeof(TConsumer),
        };

        Func<Task> pipeline = () => consumer.ConsumeAsync(context, cancellationToken);

        foreach (var middleware in _middlewares.Reverse())
        {
            var next = pipeline;
            pipeline = () => middleware.InvokeAsync(context, next, cancellationToken);
        }

        try
        {
            await pipeline();

            await sqsClient.DeleteMessageAsync(QueueUrl, message.ReceiptHandle, cancellationToken);

            logger.LogInformation("Processed SQS message {MessageId}", message.MessageId);
        }
        catch (Exception ex)
        {
            // The message stays on the queue and becomes visible again after the visibility timeout.
            logger.LogError(ex, "Failed processing SQS message {MessageId}", message.MessageId);
        }
    }
}
