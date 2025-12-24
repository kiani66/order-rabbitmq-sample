using Order.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
