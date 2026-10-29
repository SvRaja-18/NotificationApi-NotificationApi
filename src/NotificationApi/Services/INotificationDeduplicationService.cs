using NotificationApi.Domain.Models;
using NotificationApi.DTOs;

namespace NotificationApi.Services;

public interface INotificationDeduplicationService
{
    string GenerateKey(CreateNotificationRequest request, string? explicitIdempotencyKey = null);
    Task<Notification?> FindDuplicateAsync(string deduplicationKey, CancellationToken cancellationToken = default);
}
