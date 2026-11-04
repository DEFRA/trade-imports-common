using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.TradeImports.Messaging.Consuming;
using Defra.TradeImports.Messaging.Consuming.Middleware;
using Defra.TradeImports.Tracing;
using FluentAssertions;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.TradeImports.Messaging.Tests;

public class SqsConsumerBackgroundServiceTests
{
    private const string QueueUrl = "http://localhost/000000000000/things.fifo";

    private readonly IAmazonSQS _sqs = Substitute.For<IAmazonSQS>();

    private static Message Message(string? traceId = null) =>
        new()
        {
            MessageId = "m1",
            ReceiptHandle = "receipt",
            Body = "body",
            MessageAttributes = traceId is null
                ? []
                : new Dictionary<string, MessageAttributeValue>
                {
                    [MessagingHeaders.TraceId] = new()
                    {
                        DataType = "String",
                        StringValue = traceId,
                    },
                },
        };

    private SqsConsumerBackgroundService<TestConsumer> CreateService(
        TestConsumer consumer,
        IEnumerable<IConsumeMiddleware>? middlewares = null,
        bool enabled = true
    )
    {
        var services = new ServiceCollection().AddScoped(_ => consumer).BuildServiceProvider();
        return new SqsConsumerBackgroundService<TestConsumer>(
            new SqsConsumerOptions { QueueUrl = QueueUrl, Enabled = enabled },
            _sqs,
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SqsConsumerBackgroundService<TestConsumer>>.Instance,
            middlewares
        );
    }

    [Fact]
    public async Task ProcessMessage_WhenTheConsumerSucceeds_DeletesTheMessage()
    {
        var consumer = new TestConsumer();

        await CreateService(consumer).ProcessMessageAsync(Message(), CancellationToken.None);

        consumer.Received.Should().ContainSingle().Which.Body.Should().Be("body");
        await _sqs.Received(1)
            .DeleteMessageAsync(QueueUrl, "receipt", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessMessage_WhenTheConsumerThrows_LeavesTheMessageForRedelivery()
    {
        var consumer = new TestConsumer { Throw = true };

        await CreateService(consumer).ProcessMessageAsync(Message(), CancellationToken.None);

        await _sqs.DidNotReceiveWithAnyArgs()
            .DeleteMessageAsync(default(string)!, default!, default);
    }

    [Fact]
    public async Task ProcessMessage_RunsMiddlewareAroundTheConsumer()
    {
        var calls = new List<string>();
        var consumer = new TestConsumer { OnConsume = () => calls.Add("consumer") };

        await CreateService(
                consumer,
                [new Recording("first", calls), new Recording("second", calls)]
            )
            .ProcessMessageAsync(Message(), CancellationToken.None);

        calls.Should().Equal("first", "second", "consumer");
    }

    [Fact]
    public async Task TracingMiddleware_SetsTheMessagesTraceIdForTheConsumer()
    {
        var accessor = new TraceContextAccessor();
        var values = new HeaderPropagationValues();
        string? seenByConsumer = null;
        string? propagated = null;
        var consumer = new TestConsumer
        {
            OnConsume = () =>
            {
                seenByConsumer = accessor.Context?.TraceId;
                propagated = values.Headers?[MessagingHeaders.TraceId];
            },
        };

        await CreateService(
                consumer,
                [
                    new TracingConsumeMiddleware(
                        accessor,
                        values,
                        NullLogger<TracingConsumeMiddleware>.Instance
                    ),
                ]
            )
            .ProcessMessageAsync(Message("trace-1"), CancellationToken.None);

        seenByConsumer.Should().Be("trace-1");
        propagated.Should().Be("trace-1");
    }

    [Fact]
    public async Task Execute_WhenDisabled_NeverPolls()
    {
        var service = CreateService(new TestConsumer(), enabled: false);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        await _sqs.DidNotReceiveWithAnyArgs()
            .ReceiveMessageAsync(default(ReceiveMessageRequest)!, default);
    }

    [Fact]
    public async Task Execute_WhenTheQueueDoesNotExist_Stops()
    {
        _sqs.ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns<ReceiveMessageResponse>(_ => throw new QueueDoesNotExistException("missing"));
        var service = CreateService(new TestConsumer());

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        service.ExecuteTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_PollsWithTheConfiguredOptions()
    {
        ReceiveMessageRequest? request = null;
        using var stop = new CancellationTokenSource();
        _sqs.ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                request = call.Arg<ReceiveMessageRequest>();
                stop.Cancel();
                return new ReceiveMessageResponse { Messages = [] };
            });
        var services = new ServiceCollection().AddScoped<TestConsumer>().BuildServiceProvider();
        var service = new SqsConsumerBackgroundService<TestConsumer>(
            new SqsConsumerOptions
            {
                QueueUrl = QueueUrl,
                MaxMessages = 5,
                WaitTimeSeconds = 7,
                VisibilityTimeoutSeconds = 120,
            },
            _sqs,
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SqsConsumerBackgroundService<TestConsumer>>.Instance
        );

        await service.StartAsync(stop.Token);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));

        request!.QueueUrl.Should().Be(QueueUrl);
        request.MaxNumberOfMessages.Should().Be(5);
        request.WaitTimeSeconds.Should().Be(7);
        request.VisibilityTimeout.Should().Be(120);
    }

    public sealed class TestConsumer : IMessageConsumer
    {
        public List<MessageContext> Received { get; } = [];

        public bool Throw { get; init; }

        public Action? OnConsume { get; init; }

        public Task ConsumeAsync(
            MessageContext context,
            CancellationToken cancellationToken = default
        )
        {
            Received.Add(context);
            OnConsume?.Invoke();
            return Throw
                ? throw new InvalidOperationException("consumer failed")
                : Task.CompletedTask;
        }
    }

    private sealed class Recording(string name, List<string> calls) : IConsumeMiddleware
    {
        public Task InvokeAsync(
            MessageContext context,
            Func<Task> next,
            CancellationToken cancellationToken = default
        )
        {
            calls.Add(name);
            context.ConsumerType.Should().Be<TestConsumer>();
            return next();
        }
    }
}
