namespace Order.Contracts.Events;

public record OrderCreatedEvent(
    Guid OrderId,
    decimal Amount,
    DateTime CreatedAtUtc
);
