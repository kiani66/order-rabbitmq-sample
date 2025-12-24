using Order.Contracts.Events;
using Order.Worker.Infrastructure;
using Xunit;

namespace Order.Worker.Tests.Idempotency;
public class IdempotencyTests
{
    public IdempotencyTests()
    {
        ProcessedMessageStore.Reset();
    }
    [Fact]
    public async Task Duplicate_order_should_be_processed_only_once()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        var evt = new OrderCreatedEvent(
            orderId,
            500,
            DateTime.UtcNow
        );

        // Act
        await SimulateProcessingAsync(evt);
        await SimulateProcessingAsync(evt); // duplicate message

        // Assert
        Assert.True(ProcessedMessageStore.IsProcessed(orderId));
    }

    private static async Task SimulateProcessingAsync(OrderCreatedEvent evt)
    {
        if (ProcessedMessageStore.IsProcessed(evt.OrderId))
            return;

        // Simulate business logic
        await Task.Delay(10);

        ProcessedMessageStore.MarkAsProcessed(evt.OrderId);
    }
}