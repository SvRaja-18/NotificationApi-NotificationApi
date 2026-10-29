using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;
using NotificationApi.DTOs;
using NotificationApi.Repositories;
using NotificationApi.Validation;

namespace NotificationApi.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly INotificationDeduplicationService _deduplicationService;
    private readonly CreateNotificationRequestValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        INotificationDeduplicationService deduplicationService,
        CreateNotificationRequestValidator validator,
        ILogger<NotificationService> logger,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _deduplicationService = deduplicationService;
        _validator = validator;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CreateNotificationResult> CreateAsync(
        CreateNotificationRequest request,
        string? explicitIdempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Validation
        var validation = _validator.Validate(request);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Validation failed for notification request from customer {CustomerId}", request.CustomerId);
            return new CreateNotificationResult(null, false, false, validation);
        }

        // 2. Duplicate detection
        var deduplicationKey = _deduplicationService.GenerateKey(request, explicitIdempotencyKey);
        var existingDuplicate = await _deduplicationService.FindDuplicateAsync(deduplicationKey, cancellationToken);
        if (existingDuplicate is not null)
        {
            _logger.LogInformation(
                "Duplicate request detected for customer {CustomerId}. Returning existing notification {Id}",
                existingDuplicate.CustomerId,
                existingDuplicate.Id);

            return new CreateNotificationResult(
                NotificationResponse.FromDomain(existingDuplicate, isDuplicate: true),
                IsCreated: false,
                IsDuplicate: true,
                ValidationResult: null);
        }

        // 3. Entity construction
        var nowUtc = _timeProvider.GetUtcNow();
        var type = Enum.Parse<NotificationType>(request.Type!, ignoreCase: true);
        var priority = Enum.Parse<NotificationPriority>(request.Priority!, ignoreCase: true);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId!.Trim(),
            Type = type,
            Recipient = request.Recipient!.Trim(),
            Subject = request.Subject?.Trim() ?? string.Empty,
            Message = request.Message!.Trim(),
            Priority = priority,
            RequestedDeliveryTime = request.RequestedDeliveryTime ?? nowUtc,
            CreatedAt = nowUtc,
            Status = NotificationStatus.Pending,
            DeduplicationKey = deduplicationKey
        };

        // 4. Persistence
        await _repository.AddAsync(notification, cancellationToken);

        _logger.LogInformation(
            "Notification {Id} created for customer {CustomerId} (Priority: {Priority}, Delivery: {DeliveryTime})",
            notification.Id,
            notification.CustomerId,
            notification.Priority,
            notification.RequestedDeliveryTime);

        return new CreateNotificationResult(
            NotificationResponse.FromDomain(notification, isDuplicate: false),
            IsCreated: true,
            IsDuplicate: false,
            ValidationResult: null);
    }

    public async Task<NotificationResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : NotificationResponse.FromDomain(entity);
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByCustomerIdAsync(customerId, cancellationToken);
        return entities.Select(e => NotificationResponse.FromDomain(e)).ToList();
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetAllAsync(
        NotificationStatus? status = null,
        string? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync(status, customerId, cancellationToken);
        return entities.Select(e => NotificationResponse.FromDomain(e)).ToList();
    }
}
