using Microsoft.AspNetCore.Mvc;
using Order.Api.Messaging;
using Order.Contracts.Events;

namespace Order.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IEventPublisher _publisher;

    public OrdersController(IEventPublisher publisher)
    {
        _publisher = publisher;
    }

    public OrdersController()
    {
        _publisher = new RabbitMqPublisher();
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var evt = new OrderCreatedEvent(
            Guid.NewGuid(),
            1000,
            DateTime.UtcNow
        );

        await _publisher.PublishAsync(evt);

        return Ok(new
        {
            Message = "Order created and published",
            OrderId = evt.OrderId
        });
    }
}