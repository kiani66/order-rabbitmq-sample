namespace Order.Worker.Messaging;

public interface IEventConsumer
{
    Task StartAsync(CancellationToken cancellationToken);
}
