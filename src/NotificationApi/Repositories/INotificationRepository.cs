using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;

namespace NotificationApi.Repositories;

public interface INotificationRepository
{
    Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetAllAsync(NotificationStatus? status = null, string? customerId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetPendingEligibleAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default);
    Task<Notification?> FindDuplicateAsync(string deduplicationKey, DateTimeOffset createdAfterUtc, CancellationToken cancellationToken = default);
    Task<bool> TryClaimForProcessingAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(Guid id, NotificationStatus finalStatus, DateTimeOffset processedAt, string? failureReason = null, CancellationToken cancellationToken = default);
}
