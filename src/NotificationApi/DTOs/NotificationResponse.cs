using NotificationApi.Domain.Models;

namespace NotificationApi.DTOs;

public class NotificationResponse
{
    public Guid Id { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Recipient { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public DateTimeOffset RequestedDeliveryTime { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? ProcessedAt { get; init; }
    public string? FailureReason { get; init; }
    public int AttemptCount { get; init; }
    public bool IsDuplicate { get; init; }

    public static NotificationResponse FromDomain(Notification notification, bool isDuplicate = false)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            CustomerId = notification.CustomerId,
            Type = notification.Type.ToString(),
            Recipient = notification.Recipient,
            Subject = notification.Subject,
            Message = notification.Message,
            Priority = notification.Priority.ToString(),
            RequestedDeliveryTime = notification.RequestedDeliveryTime,
            CreatedAt = notification.CreatedAt,
            Status = notification.Status.ToString(),
            ProcessedAt = notification.ProcessedAt,
            FailureReason = notification.FailureReason,
            AttemptCount = notification.AttemptCount,
            IsDuplicate = isDuplicate
        };
    }
}
