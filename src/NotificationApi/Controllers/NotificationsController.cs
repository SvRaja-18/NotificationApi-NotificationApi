using Microsoft.AspNetCore.Mvc;
using NotificationApi.Domain.Enums;
using NotificationApi.DTOs;
using NotificationApi.Services;

namespace NotificationApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly INotificationProcessor _notificationProcessor;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        INotificationProcessor notificationProcessor,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _notificationProcessor = notificationProcessor;
        _logger = logger;
    }

    /// <summary>
    /// Accepts and validates a new customer notification request.
    /// Handles duplicate requests idempotently.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateNotificationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _notificationService.CreateAsync(request, idempotencyKey, cancellationToken);

        if (result.ValidationResult is not null && !result.ValidationResult.IsValid)
        {
            var problemDetails = new ValidationProblemDetails(result.ValidationResult.Errors)
            {
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Please refer to the errors property for detailed validation failures."
            };
            return BadRequest(problemDetails);
        }

        if (result.IsDuplicate)
        {
            Response.Headers.Append("X-Duplicate-Detected", "true");
            return Ok(result.Response);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Response!.Id },
            result.Response);
    }

    /// <summary>
    /// Retrieves a notification by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var notification = await _notificationService.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Notification Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Notification with ID '{id}' was not found."
            });
        }

        return Ok(notification);
    }

    /// <summary>
    /// Retrieves all notifications for a specific customer.
    /// </summary>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCustomer(string customerId, CancellationToken cancellationToken)
    {
        var notifications = await _notificationService.GetByCustomerIdAsync(customerId, cancellationToken);
        return Ok(notifications);
    }

    /// <summary>
    /// Retrieves all notifications, with optional filters for customerId and status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? customerId,
        [FromQuery] NotificationStatus? status,
        CancellationToken cancellationToken)
    {
        var notifications = await _notificationService.GetAllAsync(status, customerId, cancellationToken);
        return Ok(notifications);
    }

    /// <summary>
    /// Triggers processing for all currently eligible pending notifications.
    /// </summary>
    [HttpPost("process")]
    [ProducesResponseType(typeof(ProcessNotificationsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessPending(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual processing triggered for pending notifications.");
        var result = await _notificationProcessor.ProcessPendingAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Triggers immediate processing for a specific notification by ID.
    /// </summary>
    [HttpPost("{id:guid}/process")]
    [ProducesResponseType(typeof(NotificationProcessItemResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessSingle(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notificationProcessor.ProcessSingleAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Notification Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Notification with ID '{id}' was not found or was not in Pending state."
            });
        }

        return Ok(result);
    }
}
