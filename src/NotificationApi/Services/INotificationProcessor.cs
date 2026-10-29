using NotificationApi.DTOs;

namespace NotificationApi.Services;

public interface INotificationProcessor
{
    Task<ProcessNotificationsResult> ProcessPendingAsync(CancellationToken cancellationToken = default);
    Task<NotificationProcessItemResult?> ProcessSingleAsync(Guid id, CancellationToken cancellationToken = default);
}
