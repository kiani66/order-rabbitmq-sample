using Microsoft.Extensions.DependencyInjection;
using Order.Worker.Messaging;

var services = new ServiceCollection();

services.AddSingleton<IEventConsumer, RabbitMqConsumer>();

var provider = services.BuildServiceProvider();

var consumer = provider.GetRequiredService<IEventConsumer>();

await consumer.StartAsync(CancellationToken.None);
