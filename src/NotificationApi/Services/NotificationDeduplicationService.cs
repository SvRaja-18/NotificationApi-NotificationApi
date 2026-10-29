using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NotificationApi.Configuration;
using NotificationApi.Domain.Models;
using NotificationApi.DTOs;
using NotificationApi.Repositories;

namespace NotificationApi.Services;

public class NotificationDeduplicationService : INotificationDeduplicationService
{
    private readonly INotificationRepository _repository;
    private readonly IOptionsMonitor<NotificationProcessingOptions> _options;
    private readonly TimeProvider _timeProvider;

    public NotificationDeduplicationService(
        INotificationRepository repository,
        IOptionsMonitor<NotificationProcessingOptions> options,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string GenerateKey(CreateNotificationRequest request, string? explicitIdempotencyKey = null)
    {
        var canonicalString = new StringBuilder()
            .Append(explicitIdempotencyKey?.Trim() ?? string.Empty).Append('|')
            .Append(request.CustomerId?.Trim().ToLowerInvariant() ?? string.Empty).Append('|')
            .Append(request.Type?.Trim().ToLowerInvariant() ?? string.Empty).Append('|')
            .Append(request.Recipient?.Trim().ToLowerInvariant() ?? string.Empty).Append('|')
            .Append(request.Subject?.Trim() ?? string.Empty).Append('|')
            .Append(request.Message?.Trim() ?? string.Empty).Append('|')
            .Append(request.Priority?.Trim().ToLowerInvariant() ?? string.Empty).Append('|')
            .Append(request.RequestedDeliveryTime?.ToUniversalTime().ToString("O") ?? string.Empty)
            .ToString();

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalString));
        return Convert.ToHexString(hashBytes);
    }

    public async Task<Notification?> FindDuplicateAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        var deduplicationWindow = _options.CurrentValue.DeduplicationWindow;
        var createdAfterUtc = _timeProvider.GetUtcNow() - deduplicationWindow;

        return await _repository.FindDuplicateAsync(deduplicationKey, createdAfterUtc, cancellationToken);
    }
}
