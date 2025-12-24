# Order Processing System with RabbitMQ

A production-ready .NET 8 microservices implementation demonstrating event-driven architecture using RabbitMQ for asynchronous order processing with built-in retry logic and dead-letter queue handling.

## Architecture Overview

```
┌─────────────┐         ┌──────────────┐         ┌─────────────┐
│             │         │              │         │             │
│  Order.Api  ├────────►│   RabbitMQ   ├────────►│Order.Worker │
│  (Producer) │         │   (Broker)   │         │ (Consumer)  │
│             │         │              │         │             │
└─────────────┘         └──────────────┘         └─────────────┘
                               │
                               ├─► orders (main queue)
                               └─► orders.dlq (dead-letter queue)
```

## Projects

### Order.Api
ASP.NET Core Web API that publishes order creation events to RabbitMQ.

**Key Features:**
- RESTful endpoint for order creation
- Event publishing via `IEventPublisher` abstraction
- Dependency injection with singleton lifetime management

### Order.Worker
Background service that consumes and processes order events from RabbitMQ.

**Key Features:**
- Asynchronous event consumption with `AsyncEventingBasicConsumer`
- Automatic retry mechanism (up to 3 attempts)
- Dead-letter queue (DLQ) for failed messages
- Idempotent processing with in-memory deduplication
- Manual message acknowledgment for reliability

### Order.Contracts
Shared contract library containing event definitions.

**Key Components:**
- `OrderCreatedEvent` - Immutable record for order creation events

## Prerequisites

- .NET 8.0 SDK
- Docker & Docker Compose
- IDE: Visual Studio 2022, Rider, or VS Code

## Getting Started

### 1. Start RabbitMQ

```bash
docker-compose up -d
```

Access RabbitMQ Management UI:
- URL: http://localhost:15672
- Username: `guest`
- Password: `guest`

### 2. Run the Worker

```bash
cd Order.Worker
dotnet run
```

### 3. Run the API

```bash
cd Order.Api
dotnet run
```

The API will be available at `http://localhost:5120` with Swagger UI at `http://localhost:5120/swagger`

### 4. Create an Order

```bash
curl -X POST http://localhost:5120/api/orders
```

Or use the provided `Order.Api.http` file with REST Client extension.

## Key Implementation Details

### Message Flow

1. **Publishing** (Order.Api)
   - Controller receives HTTP POST
   - Creates `OrderCreatedEvent`
   - Publishes to `orders` queue via `RabbitMqPublisher`

2. **Consumption** (Order.Worker)
   - `RabbitMqConsumer` listens on `orders` queue
   - Deserializes message to `OrderCreatedEvent`
   - Checks for duplicate processing
   - Processes order (simulates business logic)
   - Acknowledges message on success

3. **Error Handling**
   - On failure: Increments retry count in message header
   - Republishes to `orders` queue (up to 3 attempts)
   - After 3 failures: Rejects message → moves to DLQ

### Retry Mechanism

```csharp
// Retry count stored in message headers
x-retry: 0 → 1 → 2 → 3 → DLQ
```

The worker tracks retry attempts using the `x-retry` header and automatically routes exhausted messages to the dead-letter queue.

### Queue Configuration

**Main Queue (`orders`)**
- Durable: `true`
- Dead-letter exchange: `""`
- Dead-letter routing key: `orders.dlq`

**Dead-Letter Queue (`orders.dlq`)**
- Durable: `true`
- Manual inspection and reprocessing required

### Idempotency

The `ProcessedMessageStore` maintains an in-memory `HashSet<Guid>` to prevent duplicate processing of the same order across retries.

**Limitation:** State is lost on worker restart. For production, use:
- Redis
- SQL Server with unique constraints
- Distributed cache

## Configuration

### RabbitMQ Connection

Modify connection settings in `RabbitMqPublisher.cs` and `RabbitMqConsumer.cs`:

```csharp
var factory = new ConnectionFactory
{
    HostName = "localhost",  // Change for production
    Port = 5672,
    UserName = "guest",
    Password = "guest"
};
```

**Best Practice:** Externalize to `appsettings.json` or environment variables.

### Simulated Failure

The worker intentionally fails orders with `Amount == 1000` to demonstrate retry logic:

```csharp
if (evt.Amount == 1000)
    throw new Exception("Simulated processing failure");
```

Remove or modify this condition for production use.

## Improvements for Production

### 1. Configuration Management
```csharp
// appsettings.json
{
  "RabbitMQ": {
    "HostName": "rabbitmq.prod.company.com",
    "Port": 5672,
    "UserName": "orderservice",
    "Password": "***",
    "QueueName": "orders"
  }
}
```

### 2. Connection Resilience
- Implement connection pooling
- Add automatic reconnection with exponential backoff
- Use `IConnectionFactory` with topology configuration

### 3. Persistent State
Replace `ProcessedMessageStore` with:
```csharp
public interface IProcessedMessageStore
{
    Task<bool> IsProcessedAsync(Guid orderId);
    Task MarkAsProcessedAsync(Guid orderId);
}
```

Implementations:
- Redis: Fast, distributed
- SQL Server: Transactional consistency
- Azure Table Storage: Cost-effective for high volume

### 4. Observability
```csharp
using var activity = ActivitySource.StartActivity("ProcessOrder");
activity?.SetTag("order.id", evt.OrderId);
activity?.SetTag("order.amount", evt.Amount);

_logger.LogInformation(
    "Processing order {OrderId} with amount {Amount}",
    evt.OrderId, evt.Amount
);
```

### 5. Graceful Shutdown
```csharp
public async Task StopAsync(CancellationToken cancellationToken)
{
    await _channel.CloseAsync();
    await _connection.CloseAsync();
}
```

### 6. Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddRabbitMQ(rabbitConnectionString);
```

### 7. Message Versioning
```csharp
public record OrderCreatedEvent(
    Guid OrderId,
    decimal Amount,
    DateTime CreatedAtUtc,
    int Version = 1  // Schema versioning
);
```

## Common Issues

### Connection Refused
**Cause:** RabbitMQ container not running

**Solution:**
```bash
docker-compose ps
docker-compose up -d rabbitmq
```

### Messages Not Being Consumed
**Cause:** Queue not declared or consumer not started

**Solution:**
1. Check RabbitMQ Management UI for queue status
2. Verify worker console output for errors
3. Ensure `autoAck: false` with proper acknowledgment

### Duplicate Processing
**Cause:** Worker restarted or in-memory store cleared

**Solution:** Implement persistent deduplication store (Redis/SQL)

## Testing

### Manual Testing
1. Start all services (RabbitMQ, Worker, API)
2. POST to `/api/orders` endpoint
3. Observe worker console output
4. Check RabbitMQ Management UI for queue depths

### Integration Testing
```csharp
[Fact]
public async Task OrderCreated_ShouldPublishToQueue()
{
    // Arrange
    var testServer = new TestServer(/* ... */);
    
    // Act
    var response = await testServer.CreateClient()
        .PostAsync("/api/orders", null);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    // Verify message in RabbitMQ
}
```

## Performance Considerations

### Throughput Optimization
- **Prefetch Count:** Set `channel.BasicQos(0, 10, false)` for parallel processing
- **Connection Pooling:** Reuse connections and channels
- **Batch Processing:** Process multiple messages per transaction

### Resource Management
- Dispose connections and channels properly
- Monitor memory usage with large message volumes
- Implement circuit breakers for downstream dependencies

## License

MIT License - see LICENSE file for details

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/improvement`)
3. Commit changes (`git commit -am 'Add new feature'`)
4. Push to branch (`git push origin feature/improvement`)
5. Create Pull Request

## Support

For issues and questions:
- GitHub Issues: [Create an issue](https://github.com/yourusername/OrderRabbitMqSample/issues)
- Documentation: [RabbitMQ .NET Client](https://www.rabbitmq.com/dotnet.html)

## Acknowledgments

- Built with [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client/) v7.2.0
- ASP.NET Core 8.0
- Inspired by event-driven microservices patterns
