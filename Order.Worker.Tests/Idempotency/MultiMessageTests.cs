using Order.Contracts.Events;
using Order.Worker.Infrastructure;
using Xunit;

namespace Order.Worker.Tests.Idempotency;

public class MultiMessageTests
{
    public MultiMessageTests()
    {
        ProcessedMessageStore.Reset();
    }
    [Fact]
    public async Task Multiple_orders_should_all_be_processed()
    {
        // Arrange
        var events = Enumerable.Range(1, 5)
            .Select(_ => new OrderCreatedEvent(
                Guid.NewGuid(),
                100,
                DateTime.UtcNow))
            .ToList();

        // Act
        foreach (var evt in events)
        {
            await SimulateProcessingAsync(evt);
        }

        // Assert
        foreach (var evt in events)
        {
            Assert.True(
                ProcessedMessageStore.IsProcessed(evt.OrderId),
                $"Order {evt.OrderId} was not processed"
            );
        }
    }

    private static async Task SimulateProcessingAsync(OrderCreatedEvent evt)
    {
        if (ProcessedMessageStore.IsProcessed(evt.OrderId))
            return;

        await Task.Delay(5);
        ProcessedMessageStore.MarkAsProcessed(evt.OrderId);
    }
}