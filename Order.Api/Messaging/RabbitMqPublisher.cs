using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Order.Api.Messaging;
public class RabbitMqPublisher : IEventPublisher
{
    private const string QueueName = "orders";

    public async Task PublishAsync<T>(T message)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: QueueName,
            body: body
        );
    }
   
}