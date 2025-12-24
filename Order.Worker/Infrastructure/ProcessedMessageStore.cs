namespace Order.Worker.Infrastructure;

public static class ProcessedMessageStore
{
    private static readonly HashSet<Guid> _processedOrders = new();

    public static bool IsProcessed(Guid orderId)
        => _processedOrders.Contains(orderId);

    public static void MarkAsProcessed(Guid orderId)
        => _processedOrders.Add(orderId);

    public static void Reset()
      =>  _processedOrders.Clear();
}

