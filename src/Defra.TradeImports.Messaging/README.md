# Defra.TradeImports.Messaging

SNS publishing and SQS consuming for CDP services. Messages go through a middleware pipeline that
records metrics, carries the CDP trace id (`x-cdp-request-id`) and logs. This is the pattern from
trade-gateway-publisher, packaged so services share it.

## Registration

```csharp
// The SNS and SQS clients, publisher and middleware. Also calls AddTraceContextAccessor, so
// "TraceHeader" must be configured (e.g. "x-cdp-request-id").
services.AddMessaging(configuration, options => options.MeterName = "My.Service.Meter");

// A hosted service that long-polls a queue. The consumer is resolved from a new DI scope for
// each message, so it can use scoped services. It is registered scoped if it is not registered already.
services.AddConsumer<OrderConsumer>(sp => new SqsConsumerOptions
{
    QueueUrl = sp.GetRequiredService<IOptions<MyOptions>>().Value.QueueUrl,
    VisibilityTimeoutSeconds = 120,   // must cover the slowest consume
    Enabled = true,                   // read at start, so test configuration can turn it off
});

// Or, with the defaults:
services.AddConsumer<OrderConsumer>(sp => "https://sqs.eu-west-2.amazonaws.com/123/orders.fifo");
```

Use the same `MeterName` that your EMF exporter exports (see `Defra.TradeImports.EmfExporter`), so
the metrics reach CloudWatch. The metrics are `MessagingPublish`, `MessagingPublishErrors`,
`MessagingPublishActive`, `MessagingPublishDuration` and the matching `MessagingConsume*` set.

## Publishing

```csharp
public sealed record OrderPlaced(string OrderId) : IMessage
{
    [JsonIgnore] public string DuplicationId => OrderId;   // SNS MessageDeduplicationId
    [JsonIgnore] public string? MessageGroupId => OrderId; // optional; defaults to subject, then a new id
}

await publisher.PublishAsync(topicArn, new OrderPlaced("123"), cancellationToken: ct);
```

The message is serialised with `System.Text.Json` defaults. Headers are sent as string message
attributes. The trace id is added from the current `ITraceContext` (set by consumers and scheduled
jobs), or else from the trace header propagated from the current HTTP request.

The registered SNS client is a `ResilientSnsClient`. `PublishBatchAsync` retries transient AWS
errors and transiently failed entries. Single publishes are not retried.

## Consuming

```csharp
public class OrderConsumer(IOrderService orders) : IMessageConsumer
{
    public async Task ConsumeAsync(MessageContext context, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<OrderPlaced>(context.Body)!;
        await orders.Handle(message, ct);
    }
}
```

A message is deleted only when `ConsumeAsync` completes. When it throws, the message becomes
visible again after the visibility timeout, and is redelivered until the queue's redrive policy
moves it to the dead-letter queue. Before the consumer runs, the trace id from the message becomes
the current `ITraceContext` and the propagated trace header, so outbound HTTP calls (with
`.AddHeaderPropagation()`) and re-published messages carry it on.

Add your own `IConsumeMiddleware` or `IPublishMiddleware` by registering them as singletons.

## Several topics and queues

Register one publisher per topic and one consumer per queue. The details can come from code or from
configuration sections, which are validated at start-up:

```json
"Messaging": {
  "Publishers": {
    "Orders":   { "TopicArn": "arn:aws:sns:eu-west-2:123:orders.fifo" },
    "Audit":    { "TopicArn": "arn:aws:sns:eu-west-2:123:audit.fifo" }
  },
  "Consumers": {
    "Orders":   { "QueueUrl": "https://sqs.eu-west-2.amazonaws.com/123/orders.fifo", "VisibilityTimeoutSeconds": 120 },
    "Refunds":  { "QueueUrl": "https://sqs.eu-west-2.amazonaws.com/123/refunds.fifo", "Enabled": false }
  }
}
```

```csharp
var messaging = configuration.GetSection("Messaging");

// One publisher per message type, injected as ITopicPublisher<OrderPlaced>.
services.AddPublisher<OrderPlaced>(messaging.GetSection("Publishers:Orders"));

// The same message type to several topics: give each a key, then inject with
// [FromKeyedServices("audit")] ITopicPublisher<OrderPlaced>.
services.AddPublisher<OrderPlaced>(messaging.GetSection("Publishers:Audit"), key: "audit");

// Or from code.
services.AddPublisher<RefundIssued>(sp => sp.GetRequiredService<IOptions<MyOptions>>().Value.RefundsTopicArn);

// One hosted service per call, including the same consumer type on several queues.
services.AddConsumer<OrderConsumer>(messaging.GetSection("Consumers:Orders"));
services.AddConsumer<RefundConsumer>(messaging.GetSection("Consumers:Refunds"));
```

```csharp
public class OrderService(ITopicPublisher<OrderPlaced> orders)
{
    public Task Place(Order order, CancellationToken ct) =>
        orders.PublishAsync(new OrderPlaced(order.Id), cancellationToken: ct);
}
```

`ISnsPublisher` is still available for publishing to a topic ARN chosen at run time.

## Health checks

```csharp
services.AddHealthChecks()
    .AddSqs("orders queue", sp => queueUrl)
    .AddSns("orders topic", sp => topicArn);
```

## Local development (floci)

These settings are bound from the configuration root, with the same names trade-gateway-publisher uses:

| Setting | |
|---|---|
| `USE_FLOCI` | `true` to use the endpoints below instead of the AWS SDK defaults |
| `SNS_ENDPOINT`, `SQS_ENDPOINT`, `S3_ENDPOINT` | e.g. `http://localhost:4566` |
| `AWS_REGION` | defaults to `eu-west-2` |
| `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` | default to `test` |

`FlociOptions` is public, so a service can build its other AWS clients (e.g. S3) the same way.
