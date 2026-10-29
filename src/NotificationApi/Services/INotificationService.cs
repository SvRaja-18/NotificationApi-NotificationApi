using NotificationApi.Domain.Enums;
using NotificationApi.DTOs;
using NotificationApi.Validation;

namespace NotificationApi.Services;

public record CreateNotificationResult(
    NotificationResponse? Response,
    bool IsCreated,
    bool IsDuplicate,
    ValidationResult? ValidationResult);

public interface INotificationService
{
    Task<CreateNotificationResult> CreateAsync(
        CreateNotificationRequest request,
        string? explicitIdempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationResponse>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationResponse>> GetAllAsync(
        NotificationStatus? status = null,
        string? customerId = null,
        CancellationToken cancellationToken = default);
}
