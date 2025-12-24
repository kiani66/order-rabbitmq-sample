namespace Order.Api.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(T message);
}
