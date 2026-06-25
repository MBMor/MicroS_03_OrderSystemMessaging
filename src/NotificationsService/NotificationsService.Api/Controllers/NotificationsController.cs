using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using NotificationsService.Application.Notifications.Abstractions;
using NotificationsService.Application.Notifications.Contracts;

namespace NotificationsService.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
public sealed class NotificationsController(INotificationsService notificationsService) : ControllerBase
{
    private readonly INotificationsService _notificationsService = notificationsService;

    [HttpGet]
    [MapToApiVersion(1.0)]
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
    [MapToApiVersion(1.0)]
    public async Task<ActionResult<NotificationResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var notification = await _notificationsService.GetByIdAsync(
            id,
            cancellationToken);

        if (notification is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Notification was not found.",
                detail: $"Notification with ID '{id}' was not found.");
        }

        return Ok(notification);
    }
}