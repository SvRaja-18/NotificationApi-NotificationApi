using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;
using NotificationApi.DTOs;
using NotificationApi.Providers;
using NotificationApi.Repositories;

namespace NotificationApi.Services;

public class NotificationProcessor : INotificationProcessor
{
    private readonly INotificationRepository _repository;
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly ILogger<NotificationProcessor> _logger;
    private readonly TimeProvider _timeProvider;

    public NotificationProcessor(
        INotificationRepository repository,
        IEnumerable<INotificationProvider> providers,
        ILogger<NotificationProcessor> logger,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _providers = providers;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ProcessNotificationsResult> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow();
        var pendingList = await _repository.GetPendingEligibleAsync(nowUtc, cancellationToken);

        var itemResults = new List<NotificationProcessItemResult>();
        int succeeded = 0;
        int failed = 0;

        foreach (var notification in pendingList)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var itemResult = await ProcessNotificationInternalAsync(notification, cancellationToken);
            if (itemResult is not null)
            {
                itemResults.Add(itemResult);
                if (itemResult.Success)
                    succeeded++;
                else
                    failed++;
            }
        }

        return new ProcessNotificationsResult
        {
            TotalEligible = pendingList.Count,
            Succeeded = succeeded,
            Failed = failed,
            Items = itemResults
        };
    }

    public async Task<NotificationProcessItemResult?> ProcessSingleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(id, cancellationToken);
        if (notification is null)
        {
            return null;
        }

        return await ProcessNotificationInternalAsync(notification, cancellationToken);
    }

    private async Task<NotificationProcessItemResult?> ProcessNotificationInternalAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        // Try atomic claim: transitions Pending -> Processing
        var claimed = await _repository.TryClaimForProcessingAsync(notification.Id, cancellationToken);
        if (!claimed)
        {
            // Already claimed by another worker or not in Pending status
            _logger.LogDebug("Notification {Id} could not be claimed (current status: {Status})", notification.Id, notification.Status);
            return null;
        }

        var provider = _providers.FirstOrDefault(p => p.SupportedType == notification.Type);
        if (provider is null)
        {
            var errorMessage = $"No notification provider registered for type '{notification.Type}'.";
            _logger.LogError("Processing failed for notification {Id}: {Error}", notification.Id, errorMessage);

            await _repository.MarkCompletedAsync(
                notification.Id,
                NotificationStatus.Failed,
                _timeProvider.GetUtcNow(),
                errorMessage,
                cancellationToken);

            return new NotificationProcessItemResult
            {
                Id = notification.Id,
                Recipient = notification.Recipient,
                Priority = notification.Priority.ToString(),
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        try
        {
            _logger.LogInformation("Processing notification {Id} (Priority: {Priority}, Recipient: {Recipient})",
                notification.Id, notification.Priority, notification.Recipient);

            var result = await provider.SendAsync(notification, cancellationToken);
            var completedAt = _timeProvider.GetUtcNow();

            if (result.IsSuccess)
            {
                await _repository.MarkCompletedAsync(
                    notification.Id,
                    NotificationStatus.Sent,
                    completedAt,
                    failureReason: null,
                    cancellationToken);

                return new NotificationProcessItemResult
                {
                    Id = notification.Id,
                    Recipient = notification.Recipient,
                    Priority = notification.Priority.ToString(),
                    Success = true
                };
            }
            else
            {
                var error = result.ErrorMessage ?? "Provider returned failure without details.";
                _logger.LogWarning("Provider failed for notification {Id}: {Error}", notification.Id, error);

                await _repository.MarkCompletedAsync(
                    notification.Id,
                    NotificationStatus.Failed,
                    completedAt,
                    error,
                    cancellationToken);

                return new NotificationProcessItemResult
                {
                    Id = notification.Id,
                    Recipient = notification.Recipient,
                    Priority = notification.Priority.ToString(),
                    Success = false,
                    ErrorMessage = error
                };
            }
        }
        catch (OperationCanceledException)
        {
            // Reset to pending if cancellation occurred before completion
            _logger.LogWarning("Processing was cancelled for notification {Id}", notification.Id);
            throw;
        }
        catch (Exception ex)
        {
            // Ensure application NEVER crashes due to unexpected provider failures
            var error = $"Unhandled exception while sending notification: {ex.Message}";
            _logger.LogError(ex, "Unhandled exception during processing of notification {Id}", notification.Id);

            await _repository.MarkCompletedAsync(
                notification.Id,
                NotificationStatus.Failed,
                _timeProvider.GetUtcNow(),
                error,
                cancellationToken);

            return new NotificationProcessItemResult
            {
                Id = notification.Id,
                Recipient = notification.Recipient,
                Priority = notification.Priority.ToString(),
                Success = false,
                ErrorMessage = error
            };
        }
    }
}
