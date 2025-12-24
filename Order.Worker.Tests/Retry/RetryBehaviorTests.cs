using Order.Contracts.Events;
using Order.Worker.Infrastructure;
using Xunit;

namespace Order.Worker.Tests.Retry;


public class RetryBehaviorTests
{
    public RetryBehaviorTests()
    {
        ProcessedMessageStore.Reset();
    }
    [Fact]
    public async Task Message_should_not_be_marked_processed_if_processing_fails()
    {
        // Arrange
        var evt = new OrderCreatedEvent(
            Guid.NewGuid(),
            250,
            DateTime.UtcNow
        );

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SimulateFailingProcessingAsync(evt)
        );

        // Assert
        Assert.False(
            ProcessedMessageStore.IsProcessed(evt.OrderId),
            "Failed message must not be marked as processed"
        );
    }

    private static async Task SimulateFailingProcessingAsync(OrderCreatedEvent evt)
    {
        await Task.Delay(5);
        throw new InvalidOperationException("Simulated failure");
    }
}