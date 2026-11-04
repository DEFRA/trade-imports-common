using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.TradeImports.Messaging.Consuming;
using Defra.TradeImports.Messaging.Extensions;
using Defra.TradeImports.Messaging.Publishing;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.TradeImports.Messaging.Tests;

public class MultipleRegistrationTests
{
    private readonly IAmazonSimpleNotificationService _sns =
        Substitute.For<IAmazonSimpleNotificationService>();
    private readonly List<string> _topics = [];

    public MultipleRegistrationTests()
    {
        _sns.PublishAsync(
                Arg.Do<PublishRequest>(r => _topics.Add(r.TopicArn)),
                Arg.Any<CancellationToken>()
            )
            .Returns(new PublishResponse());
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private ServiceProvider Build(IConfiguration configuration, Action<IServiceCollection> register)
    {
        var services = new ServiceCollection().AddLogging().AddMetrics();
        services.AddMessaging(
            Configuration(
                new Dictionary<string, string?>(
                    configuration.AsEnumerable().Where(kv => kv.Value is not null)
                )
                {
                    ["TraceHeader"] = "x-cdp-request-id",
                }
            )
        );
        services.AddSingleton(_sns);
        services.AddSingleton(Substitute.For<Amazon.SQS.IAmazonSQS>());
        register(services);
        return services.BuildServiceProvider();
    }

    private static List<SqsConsumerBackgroundService<T>> Consumers<T>(IServiceProvider sp)
        where T : IMessageConsumer =>
        sp.GetServices<IHostedService>().OfType<SqsConsumerBackgroundService<T>>().ToList();

    [Fact]
    public void AddConsumer_TheSameConsumerOnSeveralQueues_RunsOnEach()
    {
        using var sp = Build(
            Configuration([]),
            services =>
            {
                services.AddConsumer<SqsConsumerBackgroundServiceTests.TestConsumer>(_ =>
                    "queue-1"
                );
                services.AddConsumer<SqsConsumerBackgroundServiceTests.TestConsumer>(_ =>
                    "queue-2"
                );
                services.AddConsumer<OtherConsumer>(_ => "queue-3");
            }
        );

        Consumers<SqsConsumerBackgroundServiceTests.TestConsumer>(sp)
            .Select(c => c.QueueUrl)
            .Should()
            .BeEquivalentTo("queue-1", "queue-2");
        Consumers<OtherConsumer>(sp).Should().ContainSingle().Which.QueueUrl.Should().Be("queue-3");
    }

    [Fact]
    public void AddConsumer_FromConfiguration_BindsEachSection()
    {
        var configuration = Configuration(
            new()
            {
                ["Messaging:Consumers:Orders:QueueUrl"] = "orders.fifo",
                ["Messaging:Consumers:Orders:VisibilityTimeoutSeconds"] = "120",
                ["Messaging:Consumers:Refunds:QueueUrl"] = "refunds.fifo",
                ["Messaging:Consumers:Refunds:Enabled"] = "false",
            }
        );

        using var sp = Build(
            configuration,
            services =>
            {
                services.AddConsumer<OtherConsumer>(
                    configuration.GetSection("Messaging:Consumers:Orders")
                );
                services.AddConsumer<OtherConsumer>(
                    configuration.GetSection("Messaging:Consumers:Refunds")
                );
            }
        );

        Consumers<OtherConsumer>(sp)
            .Select(c => c.QueueUrl)
            .Should()
            .BeEquivalentTo("orders.fifo", "refunds.fifo");

        var monitor = sp.GetRequiredService<IOptionsMonitor<SqsConsumerOptions>>();
        monitor.Get("Messaging:Consumers:Orders").VisibilityTimeoutSeconds.Should().Be(120);
        monitor.Get("Messaging:Consumers:Refunds").Enabled.Should().BeFalse();
    }

    [Fact]
    public void AddConsumer_FromConfigurationWithoutAQueueUrl_FailsValidation()
    {
        var configuration = Configuration(
            new() { ["Messaging:Consumers:Orders:Enabled"] = "true" }
        );
        using var sp = Build(
            configuration,
            services =>
                services.AddConsumer<OtherConsumer>(
                    configuration.GetSection("Messaging:Consumers:Orders")
                )
        );

        var act = () =>
            sp.GetRequiredService<IOptionsMonitor<SqsConsumerOptions>>()
                .Get("Messaging:Consumers:Orders");

        act.Should().Throw<OptionsValidationException>().WithMessage("*QueueUrl*");
    }

    [Fact]
    public async Task AddPublisher_EachMessageTypeGoesToItsOwnTopic()
    {
        using var sp = Build(
            Configuration([]),
            services =>
            {
                services.AddPublisher<OrderPlaced>(_ =>
                    "arn:aws:sns:eu-west-2:000000000000:orders.fifo"
                );
                services.AddPublisher<RefundIssued>(_ =>
                    "arn:aws:sns:eu-west-2:000000000000:refunds.fifo"
                );
            }
        );

        await sp.GetRequiredService<ITopicPublisher<OrderPlaced>>()
            .PublishAsync(new OrderPlaced("1"));
        await sp.GetRequiredService<ITopicPublisher<RefundIssued>>()
            .PublishAsync(new RefundIssued("2"));

        _topics
            .Should()
            .Equal(
                "arn:aws:sns:eu-west-2:000000000000:orders.fifo",
                "arn:aws:sns:eu-west-2:000000000000:refunds.fifo"
            );
    }

    [Fact]
    public async Task AddPublisher_Keyed_SendsOneMessageTypeToSeveralTopics()
    {
        var configuration = Configuration(
            new()
            {
                ["Messaging:Publishers:Internal:TopicArn"] =
                    "arn:aws:sns:eu-west-2:000000000000:internal.fifo",
                ["Messaging:Publishers:Public:TopicArn"] =
                    "arn:aws:sns:eu-west-2:000000000000:public.fifo",
            }
        );

        using var sp = Build(
            configuration,
            services =>
            {
                services.AddPublisher<OrderPlaced>(
                    configuration.GetSection("Messaging:Publishers:Internal"),
                    "internal"
                );
                services.AddPublisher<OrderPlaced>(
                    configuration.GetSection("Messaging:Publishers:Public"),
                    "public"
                );
            }
        );

        await sp.GetRequiredKeyedService<ITopicPublisher<OrderPlaced>>("public")
            .PublishAsync(new OrderPlaced("1"));
        await sp.GetRequiredKeyedService<ITopicPublisher<OrderPlaced>>("internal")
            .PublishAsync(new OrderPlaced("2"));

        _topics
            .Should()
            .Equal(
                "arn:aws:sns:eu-west-2:000000000000:public.fifo",
                "arn:aws:sns:eu-west-2:000000000000:internal.fifo"
            );
        sp.GetService<ITopicPublisher<OrderPlaced>>()
            .Should()
            .BeNull("only keyed publishers were registered");
    }

    [Fact]
    public void AddPublisher_FromConfigurationWithoutATopic_FailsValidation()
    {
        var configuration = Configuration([]);
        using var sp = Build(
            configuration,
            services =>
                services.AddPublisher<OrderPlaced>(
                    configuration.GetSection("Messaging:Publishers:Orders")
                )
        );

        var act = () => sp.GetRequiredService<ITopicPublisher<OrderPlaced>>();

        act.Should().Throw<OptionsValidationException>().WithMessage("*TopicArn*");
    }

    [Fact]
    public void AddPublisher_WithAnEmptyTopic_SaysWhichPublisher()
    {
        using var sp = Build(
            Configuration([]),
            services => services.AddPublisher<OrderPlaced>(_ => "", "orders")
        );

        var act = () => sp.GetRequiredKeyedService<ITopicPublisher<OrderPlaced>>("orders");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*OrderPlaced publisher 'orders'*");
    }

    public sealed record OrderPlaced(string Id) : IMessage
    {
        public string DuplicationId => Id;
    }

    public sealed record RefundIssued(string Id) : IMessage
    {
        public string DuplicationId => Id;
    }

    public sealed class OtherConsumer : IMessageConsumer
    {
        public Task ConsumeAsync(
            MessageContext context,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
