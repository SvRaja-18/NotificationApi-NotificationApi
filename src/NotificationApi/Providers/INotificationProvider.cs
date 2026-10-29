using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;

namespace NotificationApi.Providers;

public interface INotificationProvider
{
    NotificationType SupportedType { get; }
    Task<ProviderResult> SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
