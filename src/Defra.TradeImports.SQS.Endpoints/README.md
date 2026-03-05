# Defra.TradeImports.SQS.Endpoints

A .NET library providing ASP.NET Core endpoints and services for managing AWS SQS Dead Letter Queues (DLQ). This package simplifies DLQ operations including redriving messages, removing specific messages, draining queues, and monitoring message counts.

## Features

- **Redrive Messages**: Move all messages from a dead letter queue back to the main queue
- **Remove Specific Messages**: Delete individual messages by message ID
- **Drain Queue**: Remove all messages from a dead letter queue
- **Get Message Count**: Retrieve the approximate number of messages in a queue
- **Ready-to-use API Endpoints**: Pre-configured minimal API endpoints with OpenAPI documentation
- **Authorization Support**: Optional policy-based authorization for endpoints
- **Comprehensive Logging**: Built-in logging for all operations

## Installation

Install the package via NuGet:

```bash
dotnet add package Defra.TradeImports.SQS.Endpoints
```

## Prerequisites

- .NET 10.0 or later
- AWS SQS access with appropriate permissions
- AWS SDK configured with credentials

## Quick Start

### 1. Register Services

Add the dead letter queue management services to your dependency injection container:

```csharp
using Defra.TradeImports.SQS.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Register AWS SQS client
builder.Services.AddAWSService<IAmazonSQS>();

// Register DLQ management services
builder.Services.AddDeadLetterQueueManagementServices();

var app = builder.Build();
```

### 2. Map Endpoints

Configure the DLQ management endpoints:

```csharp
using Defra.TradeImports.SQS.Endpoints.Endpoints;

// Basic usage
app.MapDeadLetterQueueEndpoints(
    queueName: "my-main-queue",
    dqlQueueName: "my-main-queue-dlq"
);

// With custom configuration
app.MapDeadLetterQueueEndpoints(
    queueName: "my-main-queue",
    dqlQueueName: "my-main-queue-dlq",
    pattern: "admin/dlq",              // URL pattern (default: "admin/dlq")
    policyName: "AdminPolicy",         // Authorization policy (optional)
    nameSuffix: "MainQueue",           // Endpoint name suffix (optional)
    tags: new[] { "Admin", "DLQ" }     // OpenAPI tags (optional)
);

app.Run();
```

## API Endpoints

Once configured, the following endpoints are available:

### POST /admin/dlq/redrive

Initiates a redrive operation to move all messages from the dead letter queue back to the main queue.

**Response:**
- `202 Accepted` - Redrive operation started successfully
- `500 Internal Server Error` - Operation failed

**Example:**
```bash
curl -X POST https://your-api.com/admin/dlq/redrive
```

### POST /admin/dlq/remove-message

Removes a specific message from the dead letter queue by message ID.

**Query Parameters:**
- `messageId` (required) - The SQS message ID to remove

**Response:**
- `200 OK` - Returns status message (text/plain)

**Example:**
```bash
curl -X POST "https://your-api.com/admin/dlq/remove-message?messageId=abc123"
```

### POST /admin/dlq/drain

Drains all messages from the dead letter queue (permanently deletes them).

**Response:**
- `200 OK` - Queue drained successfully
- `500 Internal Server Error` - Operation failed

**Example:**
```bash
curl -X POST https://your-api.com/admin/dlq/drain
```

### GET /admin/dlq/count

Retrieves the approximate number of messages in the dead letter queue.

**Response:**
- `200 OK` - Returns JSON with message count
  ```json
  {
    "deadLetterQueueCount": 42
  }
  ```

**Example:**
```bash
curl https://your-api.com/admin/dlq/count
```

## Service Usage

You can also use the `ISqsDeadLetterService` directly in your code:

```csharp
public class MyService
{
    private readonly ISqsDeadLetterService _dlqService;

    public MyService(ISqsDeadLetterService dlqService)
    {
        _dlqService = dlqService;
    }

    public async Task ManageDeadLetterQueue(CancellationToken cancellationToken)
    {
        // Get message count
        var count = await _dlqService.GetCount("my-queue-dlq", cancellationToken);
        
        // Redrive messages
        var success = await _dlqService.Redrive(
            sourceQueueName: "my-queue-dlq",
            destinationQueueName: "my-queue",
            cancellationToken
        );
        
        // Remove specific message
        var result = await _dlqService.Remove(
            messageId: "abc123",
            queueName: "my-queue-dlq",
            cancellationToken
        );
        
        // Drain queue
        var drained = await _dlqService.Drain("my-queue-dlq", cancellationToken);
    }
}
```

## Configuration Examples

### Multiple Queue Endpoints

```csharp
// Orders queue
app.MapDeadLetterQueueEndpoints(
    queueName: "orders-queue",
    dqlQueueName: "orders-queue-dlq",
    pattern: "admin/orders/dlq",
    nameSuffix: "Orders",
    tags: new[] { "Orders", "Admin" }
);

// Notifications queue
app.MapDeadLetterQueueEndpoints(
    queueName: "notifications-queue",
    dqlQueueName: "notifications-queue-dlq",
    pattern: "admin/notifications/dlq",
    nameSuffix: "Notifications",
    tags: new[] { "Notifications", "Admin" }
);
```

### With Authorization

```csharp
// Configure authorization policy
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Administrator"));
});

// Map endpoints with authorization
app.MapDeadLetterQueueEndpoints(
    queueName: "my-queue",
    dqlQueueName: "my-queue-dlq",
    policyName: "AdminOnly"
);
```

## AWS Permissions Required

The AWS credentials used must have the following SQS permissions:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "sqs:GetQueueUrl",
        "sqs:GetQueueAttributes",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:DeleteMessageBatch",
        "sqs:StartMessageMoveTask"
      ],
      "Resource": [
        "arn:aws:sqs:*:*:your-queue-name",
        "arn:aws:sqs:*:*:your-queue-name-dlq"
      ]
    }
  ]
}
```

## Dependencies

- **AWSSDK.Core** (4.0.3.14)
- **AWSSDK.Extensions.NETCore.Setup** (4.0.3.22)
- **AWSSDK.SQS** (4.0.2.14)
- **Microsoft.AspNetCore.App** (Framework Reference)

## License

This project is maintained by DEFRA.

## Repository

[https://github.com/DEFRA/trade-imports-common](https://github.com/DEFRA/trade-imports-common)

## Version

Current version: 0.1.1
