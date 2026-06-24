using Microsoft.AspNetCore.Mvc;
using NotificationsService.Application.Notifications.Abstractions;
using NotificationsService.Application.Notifications.Contracts;

namespace NotificationsService.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationsService notificationsService) : ControllerBase
{
    private readonly INotificationsService _notificationsService = notificationsService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<NotificationResponse>>> ListAsync(
        [FromQuery] ListNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _notificationsService.ListAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var notification = await _notificationsService.GetByIdAsync(
            id,
            cancellationToken);

        if (notification is null)
        {
            return NotFound();
        }

        return Ok(notification);
    }
}