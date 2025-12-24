using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Order.Contracts.Events;
using Order.Worker.Infrastructure;

namespace Order.Worker.Messaging;

public class RabbitMqConsumer : IEventConsumer
{
    // Entry point
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        DeclareQueues(channel);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: true);
                return;
            }
            await HandleMessageAsync(channel, ea, cancellationToken);
        };

        await channel.BasicConsumeAsync(queue: "orders", autoAck: false, consumer: consumer);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("🛑 Worker is shutting down gracefully...");
        }
    }
    //Infrastructure / Startup
    private static void DeclareQueues(IChannel channel)
    {
        IDictionary<string, object?> args = new Dictionary<string, object?>
    {
        { "x-dead-letter-exchange", "" },
        { "x-dead-letter-routing-key", "orders.dlq" }
    };

        channel.QueueDeclareAsync(
            queue: "orders",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args
        ).GetAwaiter().GetResult();

        channel.QueueDeclareAsync(
            queue: "orders.dlq",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        ).GetAwaiter().GetResult();
    }
    //  Message orchestration
    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        try
        {
            var evt = DeserializeMessage(ea.Body.ToArray());

            await ProcessMessageAsync(evt, cancellationToken);

            await channel.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception)
        {
            await HandleFailureAsync(channel, ea);
        }
    }
    //  Business logic
    private async Task ProcessMessageAsync(OrderCreatedEvent evt, CancellationToken cancellationToken)
    {
        if (ProcessedMessageStore.IsProcessed(evt.OrderId))
        {
            Console.WriteLine($"⚠️ Order {evt.OrderId} already processed. Skipping.");
            return;
        }

        Console.WriteLine($"📥 Order received: {evt.OrderId}");

        // شبیه‌سازی خطای واقعی
        if (evt.Amount == 1000)
            throw new Exception("Simulated processing failure");

        await Task.Delay(1000, cancellationToken);
        ProcessedMessageStore.MarkAsProcessed(evt.OrderId);
    }
    //  Serialization
    private OrderCreatedEvent DeserializeMessage(byte[] body)
    {
        var json = Encoding.UTF8.GetString(body);
        return JsonSerializer.Deserialize<OrderCreatedEvent>(json)!;
    }
    //  Error handling
    private async Task HandleFailureAsync(IChannel channel, BasicDeliverEventArgs ea)
    {
        var retryCount = GetRetryCount(ea);

        if (retryCount < 3)
        {
            await RetryMessageAsync(channel, ea, retryCount + 1);
            await channel.BasicAckAsync(ea.DeliveryTag, false);
        }
        else
        {
            await MoveToDlqAsync(channel, ea);
        }
    }
    //  Retry helpers
    private int GetRetryCount(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties?.Headers == null)
            return 0;

        if (!ea.BasicProperties.Headers.TryGetValue("x-retry", out var header))
            return 0;

        if (header is not byte[] bytes)
            return 0;

        return int.Parse(Encoding.UTF8.GetString(bytes));
    }
    private async Task RetryMessageAsync(IChannel channel, BasicDeliverEventArgs ea, int retryCount)
    {
        Console.WriteLine($"🔁 Retry {retryCount}");

        var props = new BasicProperties
        {
            Headers = new Dictionary<string, object?>
        {
            { "x-retry", Encoding.UTF8.GetBytes(retryCount.ToString()) }
        }
        };

        await channel.BasicPublishAsync(
        exchange: "",
        routingKey: "orders",
        mandatory: false,
        basicProperties: props,
        body: ea.Body
    );

    }
    //  DLQ
    private async Task MoveToDlqAsync(IChannel channel, BasicDeliverEventArgs ea)
    {
        Console.WriteLine("☠️ Message moved to DLQ");

        await channel.BasicRejectAsync(
            ea.DeliveryTag,
            requeue: false
        );
    }
}