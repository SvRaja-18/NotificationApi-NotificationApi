using System.Collections.Concurrent;
using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;

namespace NotificationApi.Repositories;

public class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<Guid, Notification> _store = new();
    private readonly object _stateLock = new();

    public Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _store.TryAdd(notification.Id, notification);
        return Task.FromResult(notification);
    }

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var notification);
        return Task.FromResult(notification);
    }

    public Task<IReadOnlyList<Notification>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var list = _store.Values
            .Where(n => string.Equals(n.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Notification>>(list);
    }

    public Task<IReadOnlyList<Notification>> GetAllAsync(
        NotificationStatus? status = null,
        string? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _store.Values.AsEnumerable();

        if (status.HasValue)
        {
            query = query.Where(n => n.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            query = query.Where(n => string.Equals(n.CustomerId, customerId, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.OrderByDescending(n => n.CreatedAt).ToList();
        return Task.FromResult<IReadOnlyList<Notification>>(list);
    }

    public Task<IReadOnlyList<Notification>> GetPendingEligibleAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
    {
        var list = _store.Values
            .Where(n => n.IsEligibleForProcessing(asOfUtc))
            .OrderByDescending(n => n.Priority)
            .ThenBy(n => n.RequestedDeliveryTime)
            .ThenBy(n => n.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Notification>>(list);
    }

    public Task<Notification?> FindDuplicateAsync(string deduplicationKey, DateTimeOffset createdAfterUtc, CancellationToken cancellationToken = default)
    {
        var match = _store.Values
            .Where(n => n.DeduplicationKey == deduplicationKey && n.CreatedAt >= createdAfterUtc)
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(match);
    }

    public Task<bool> TryClaimForProcessingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (!_store.TryGetValue(id, out var notification))
            {
                return Task.FromResult(false);
            }

            if (notification.Status != NotificationStatus.Pending)
            {
                return Task.FromResult(false);
            }

            notification.Status = NotificationStatus.Processing;
            notification.AttemptCount++;
            return Task.FromResult(true);
        }
    }

    public Task MarkCompletedAsync(
        Guid id,
        NotificationStatus finalStatus,
        DateTimeOffset processedAt,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_store.TryGetValue(id, out var notification))
            {
                notification.Status = finalStatus;
                notification.ProcessedAt = processedAt;
                notification.FailureReason = failureReason;
            }
        }

        return Task.CompletedTask;
    }
}
