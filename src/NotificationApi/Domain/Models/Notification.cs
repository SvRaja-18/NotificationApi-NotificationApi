using NotificationApi.Domain.Enums;

namespace NotificationApi.Domain.Models;

public class Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string CustomerId { get; init; }
    public required NotificationType Type { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
    public required NotificationPriority Priority { get; init; }
    public required DateTimeOffset RequestedDeliveryTime { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? FailureReason { get; set; }
    public int AttemptCount { get; set; }
    public string? DeduplicationKey { get; init; }

    public bool IsEligibleForProcessing(DateTimeOffset asOfUtc) =>
        Status == NotificationStatus.Pending && RequestedDeliveryTime <= asOfUtc;
}
