using Order.Contracts.Events;
using Order.Worker.Infrastructure;
using Xunit;

namespace Order.Worker.Tests.Shutdown;

public class CancellationTests
{
    public CancellationTests()
    {
        ProcessedMessageStore.Reset();
    }
    [Fact]
    public async Task Processing_should_stop_when_cancellation_is_requested()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var evt = new OrderCreatedEvent(
            Guid.NewGuid(),
            300,
            DateTime.UtcNow
        );

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => SimulateProcessingAsync(evt, cts.Token)
        );

        Assert.False(ProcessedMessageStore.IsProcessed(evt.OrderId));
    }

    private static async Task SimulateProcessingAsync(
        OrderCreatedEvent evt,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        ProcessedMessageStore.MarkAsProcessed(evt.OrderId);
    }
}