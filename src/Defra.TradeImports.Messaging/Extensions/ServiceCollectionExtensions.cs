using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Defra.TradeImports.Messaging.Consuming;
using Defra.TradeImports.Messaging.Consuming.Middleware;
using Defra.TradeImports.Messaging.Encoding;
using Defra.TradeImports.Messaging.Publishing;
using Defra.TradeImports.Messaging.Publishing.Middleware;
using Defra.TradeImports.Messaging.Resilience;
using Defra.TradeImports.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Defra.TradeImports.Messaging.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SNS publishing and the SQS consume middleware (tracing, metrics, logging), plus the
    /// SNS and SQS clients: floci when <c>USE_FLOCI</c> is true, otherwise the AWS SDK defaults.
    /// Also registers the trace context accessor, which needs <c>TraceHeader</c> configured.
    /// </summary>
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<MessagingOptions>? configure = null
    )
    {
        var options = new MessagingOptions();
        configure?.Invoke(options);

        services.AddTraceContextAccessor(configuration);
        services.AddOptions<FlociOptions>().Bind(configuration);

        services.TryAddSingleton<ISnsPublisher, SnsPublisher>();

        services.AddSingleton<IPublishMiddleware, MetricsPublishMiddleware>();
        services.AddSingleton<IPublishMiddleware, TracingPublishMiddleware>();

        services.AddSingleton<IMessageEncoder, GZipEncoder>();

        services.AddSingleton<IConsumeMiddleware, TracingConsumeMiddleware>();
        services.AddSingleton<IConsumeMiddleware, MetricsConsumeMiddleware>();
        services.AddSingleton<IConsumeMiddleware, LoggingConsumeMiddleware>();

        services.TryAddSingleton(sp => new ConsumerMetrics(
            sp.GetRequiredService<IMeterFactory>(),
            options.MeterName
        ));
        services.TryAddSingleton(sp => new PublishMetrics(
            sp.GetRequiredService<IMeterFactory>(),
            options.MeterName
        ));

        services.TryAddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ResilientSnsClient>>();
            var floci = sp.GetRequiredService<IOptions<FlociOptions>>().Value;
            if (floci.UseFloci != true)
                return new ResilientSnsClient(logger);

            return new ResilientSnsClient(
                logger,
                floci.Credentials(),
                new AmazonSimpleNotificationServiceConfig
                {
                    // https://github.com/aws/aws-sdk-net/issues/1781
                    AuthenticationRegion = floci.Region,
                    RegionEndpoint = RegionEndpoint.GetBySystemName(floci.Region),
                    ServiceURL = floci.SnsEndpoint,
                }
            );
        });

        services.TryAddSingleton<IAmazonSQS>(sp =>
        {
            var floci = sp.GetRequiredService<IOptions<FlociOptions>>().Value;
            if (floci.UseFloci != true)
                return new AmazonSQSClient();

            return new AmazonSQSClient(
                floci.Credentials(),
                new AmazonSQSConfig
                {
                    AuthenticationRegion = floci.Region,
                    RegionEndpoint = RegionEndpoint.GetBySystemName(floci.Region),
                    ServiceURL = floci.SqsEndpoint,
                }
            );
        });

        return services;
    }

    /// <summary>
    /// Runs <typeparamref name="TConsumer"/> against an SQS queue as a hosted service. The consumer is
    /// registered scoped unless already registered; it is resolved from a new scope per message.
    /// </summary>
    /// <remarks>
    /// Call once per queue. Each call adds its own hosted service, including for the same consumer
    /// type on several queues.
    /// </remarks>
    public static IServiceCollection AddConsumer<TConsumer>(
        this IServiceCollection services,
        Func<IServiceProvider, SqsConsumerOptions> optionsFactory
    )
        where TConsumer : class, IMessageConsumer
    {
        services.TryAddScoped<TConsumer>();

        // Not AddHostedService: it de-duplicates on the service type, which would silently drop a
        // second queue for the same consumer.
        services.AddSingleton<IHostedService>(sp => new SqsConsumerBackgroundService<TConsumer>(
            optionsFactory(sp),
            sp.GetRequiredService<IAmazonSQS>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<SqsConsumerBackgroundService<TConsumer>>>(),
            sp.GetServices<IConsumeMiddleware>()
        ));

        return services;
    }

    /// <inheritdoc cref="AddConsumer{TConsumer}(IServiceCollection, Func{IServiceProvider, SqsConsumerOptions})"/>
    public static IServiceCollection AddConsumer<TConsumer>(
        this IServiceCollection services,
        Func<IServiceProvider, string> queueUrlFactory
    )
        where TConsumer : class, IMessageConsumer =>
        services.AddConsumer<TConsumer>(sp => new SqsConsumerOptions
        {
            QueueUrl = queueUrlFactory(sp),
        });

    /// <summary>
    /// Runs <typeparamref name="TConsumer"/> against the queue described by a configuration section
    /// (<see cref="SqsConsumerOptions"/>: <c>QueueUrl</c>, <c>Enabled</c>, <c>VisibilityTimeoutSeconds</c>...),
    /// validated at start-up.
    /// </summary>
    public static IServiceCollection AddConsumer<TConsumer>(
        this IServiceCollection services,
        IConfigurationSection section
    )
        where TConsumer : class, IMessageConsumer
    {
        var name = section.Path;
        services
            .AddOptions<SqsConsumerOptions>(name)
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddConsumer<TConsumer>(sp =>
            sp.GetRequiredService<IOptionsMonitor<SqsConsumerOptions>>().Get(name)
        );
    }

    /// <summary>
    /// Registers an <see cref="ITopicPublisher{TMessage}"/> for one topic. With a <paramref name="key"/>
    /// it is a keyed service (<c>[FromKeyedServices(key)]</c>), so one message type can go to several
    /// topics; without one, it is the message type's default publisher.
    /// </summary>
    public static IServiceCollection AddPublisher<TMessage>(
        this IServiceCollection services,
        Func<IServiceProvider, TopicPublisherOptions?> optionsFactory,
        string? key = null
    )
        where TMessage : IMessage
    {
        ITopicPublisher<TMessage> Create(IServiceProvider sp)
        {
            var topicOptions = optionsFactory(sp);
            if (string.IsNullOrWhiteSpace(topicOptions?.TopicArn))
                throw new InvalidOperationException(
                    $"No topic ARN configured for the {typeof(TMessage).Name} publisher{(key is null ? "" : $" '{key}'")}."
                );

            return new TopicPublisher<TMessage>(sp.GetRequiredService<ISnsPublisher>(), topicOptions);
        }

        if (key is null)
            services.AddSingleton(Create);
        else
            services.AddKeyedSingleton<ITopicPublisher<TMessage>>(key, (sp, _) => Create(sp));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="ITopicPublisher{TMessage}"/> for the topic in a configuration section
    /// (<see cref="TopicPublisherOptions"/>: <c>TopicArn</c>), validated at start-up.
    /// </summary>
    public static IServiceCollection AddPublisher<TMessage>(
        this IServiceCollection services,
        IConfigurationSection section,
        string? key = null
    )
        where TMessage : IMessage
    {
        var name = section.Path;
        services
            .AddOptions<TopicPublisherOptions>(name)
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddPublisher<TMessage>(
            sp =>
                sp.GetRequiredService<IOptionsMonitor<TopicPublisherOptions>>().Get(name),
            key
        );
    }
}
