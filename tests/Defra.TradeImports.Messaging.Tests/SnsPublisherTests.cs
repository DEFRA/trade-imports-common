using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.TradeImports.Messaging.Publishing;
using Defra.TradeImports.Messaging.Publishing.Middleware;
using Defra.TradeImports.Tracing;
using FluentAssertions;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Primitives;
using NSubstitute;

namespace Defra.TradeImports.Messaging.Tests;

public class SnsPublisherTests
{
    private const string TopicArn = "arn:aws:sns:eu-west-2:000000000000:things.fifo";

    private readonly IAmazonSimpleNotificationService _sns =
        Substitute.For<IAmazonSimpleNotificationService>();
    private PublishRequest? _sent;

    public SnsPublisherTests()
    {
        _sns.PublishAsync(Arg.Do<PublishRequest>(r => _sent = r), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse());
    }

    private sealed record GroupedMessage(string Id) : IMessage
    {
        public string DuplicationId => $"dup-{Id}";

        public string MessageGroupId => $"group-{Id}";
    }

    private sealed record UngroupedMessage(string Id) : IMessage
    {
        public string DuplicationId => Id;
    }

    [Fact]
    public async Task PublishAsync_Message_SendsItsDeduplicationAndGroupIds()
    {
        await new SnsPublisher(_sns).PublishAsync(TopicArn, new GroupedMessage("1"));

        _sent!.TopicArn.Should().Be(TopicArn);
        System
            .Text.Json.JsonDocument.Parse(_sent.Message)
            .RootElement.GetProperty("Id")
            .GetString()
            .Should()
            .Be("1");
        _sent.MessageDeduplicationId.Should().Be("dup-1");
        _sent.MessageGroupId.Should().Be("group-1");
    }

    [Fact]
    public async Task PublishAsync_MessageWithoutGroup_GroupsBySubject()
    {
        await new SnsPublisher(_sns).PublishAsync(
            TopicArn,
            new UngroupedMessage("1"),
            subject: "subject"
        );

        _sent!.MessageGroupId.Should().Be("subject");
        _sent.MessageDeduplicationId.Should().Be("1");
    }

    [Fact]
    public async Task PublishAsync_WithoutGroupOrSubject_UsesANewGroupPerMessage()
    {
        await new SnsPublisher(_sns).PublishAsync(TopicArn, new UngroupedMessage("1"));

        _sent!.MessageGroupId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PublishAsync_SendsHeadersAsStringAttributes()
    {
        await new SnsPublisher(_sns).PublishAsync(
            TopicArn,
            "body",
            new Dictionary<string, string> { ["a"] = "b" }
        );

        _sent!.MessageAttributes["a"].DataType.Should().Be("String");
        _sent.MessageAttributes["a"].StringValue.Should().Be("b");
    }

    [Fact]
    public async Task PublishAsync_RunsMiddlewareInRegistrationOrder()
    {
        var calls = new List<string>();
        var publisher = new SnsPublisher(
            _sns,
            [new Recording("first", calls), new Recording("second", calls)]
        );

        await publisher.PublishAsync(TopicArn, "body");

        calls.Should().Equal("first", "second");
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData(TopicArn, "")]
    public async Task PublishAsync_RequiresTopicAndBody(string topicArn, string body)
    {
        var act = () => new SnsPublisher(_sns).PublishAsync(topicArn, body);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TracingMiddleware_PrefersTheTraceContext()
    {
        var accessor = new TraceContextAccessor
        {
            Context = new TraceContext { TraceId = "from-context" },
        };
        var values = new HeaderPropagationValues
        {
            Headers = new Dictionary<string, StringValues>
            {
                [MessagingHeaders.TraceId] = "from-header",
            },
        };

        await new SnsPublisher(_sns, [new TracingPublishMiddleware(accessor, values)]).PublishAsync(
            TopicArn,
            "body"
        );

        _sent!.MessageAttributes[MessagingHeaders.TraceId].StringValue.Should().Be("from-context");
        accessor.Context = null;
    }

    [Fact]
    public async Task TracingMiddleware_FallsBackToThePropagatedRequestHeader()
    {
        var values = new HeaderPropagationValues
        {
            Headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
            {
                [MessagingHeaders.TraceId] = "from-header",
            },
        };

        await new SnsPublisher(
            _sns,
            [new TracingPublishMiddleware(new TraceContextAccessor(), values)]
        ).PublishAsync(TopicArn, "body");

        _sent!.MessageAttributes[MessagingHeaders.TraceId].StringValue.Should().Be("from-header");
    }

    [Fact]
    public async Task TracingMiddleware_WithoutATrace_SendsNoTraceAttribute()
    {
        await new SnsPublisher(
            _sns,
            [
                new TracingPublishMiddleware(
                    new TraceContextAccessor(),
                    new HeaderPropagationValues()
                ),
            ]
        ).PublishAsync(TopicArn, "body");

        _sent!.MessageAttributes.Should().NotContainKey(MessagingHeaders.TraceId);
    }

    private sealed class Recording(string name, List<string> calls) : IPublishMiddleware
    {
        public Task InvokeAsync(
            PublishContext context,
            Func<Task> next,
            CancellationToken cancellationToken = default
        )
        {
            calls.Add(name);
            context.TopicName.Should().Be("things.fifo");
            return next();
        }
    }
}
