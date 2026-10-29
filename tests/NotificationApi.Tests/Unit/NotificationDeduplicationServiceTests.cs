using Microsoft.Extensions.Options;
using NotificationApi.Configuration;
using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;
using NotificationApi.DTOs;
using NotificationApi.Repositories;
using NotificationApi.Services;
using NotificationApi.Tests.TestHelpers;
using Xunit;

namespace NotificationApi.Tests.Unit;

public class NotificationDeduplicationServiceTests
{
    private readonly InMemoryNotificationRepository _repository;
    private readonly TestTimeProvider _timeProvider;
    private readonly NotificationDeduplicationService _service;
    private readonly DateTimeOffset _referenceTime = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    public NotificationDeduplicationServiceTests()
    {
        _repository = new InMemoryNotificationRepository();
        _timeProvider = new TestTimeProvider(_referenceTime);

        var options = new NotificationProcessingOptions
        {
            DeduplicationWindow = TimeSpan.FromMinutes(5)
        };
        var optionsMonitor = new TestOptionsMonitor<NotificationProcessingOptions>(options);

        _service = new NotificationDeduplicationService(_repository, optionsMonitor, _timeProvider);
    }

    private CreateNotificationRequest CreateSampleRequest() => new()
    {
        CustomerId = "CUST-101",
        Type = "Email",
        Recipient = "user@example.com",
        Subject = "Hello",
        Message = "Your verification code is 1234.",
        Priority = "High",
        RequestedDeliveryTime = _referenceTime
    };

    [Fact]
    public void GenerateKey_IdenticalRequests_ProduceIdenticalHash()
    {
        var req1 = CreateSampleRequest();
        var req2 = CreateSampleRequest();

        var key1 = _service.GenerateKey(req1);
        var key2 = _service.GenerateKey(req2);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateKey_DifferentCasingOrWhitespace_ProducesIdenticalHash()
    {
        var req1 = CreateSampleRequest();
        var req2 = new CreateNotificationRequest
        {
            CustomerId = " cust-101 ",
            Type = "EMAIL",
            Recipient = " USER@example.com ",
            Subject = "Hello",
            Message = "Your verification code is 1234.",
            Priority = "high",
            RequestedDeliveryTime = _referenceTime
        };

        var key1 = _service.GenerateKey(req1);
        var key2 = _service.GenerateKey(req2);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateKey_DifferentCustomerOrRecipient_ProducesDifferentHash()
    {
        var req1 = CreateSampleRequest();
        var req2 = CreateSampleRequest();
        req2.Recipient = "different@example.com";

        var key1 = _service.GenerateKey(req1);
        var key2 = _service.GenerateKey(req2);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public async Task FindDuplicateAsync_WithinWindow_ReturnsExistingNotification()
    {
        var request = CreateSampleRequest();
        var key = _service.GenerateKey(request);

        var existing = new Notification
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId!,
            Type = NotificationType.Email,
            Recipient = request.Recipient!,
            Subject = request.Subject!,
            Message = request.Message!,
            Priority = NotificationPriority.High,
            RequestedDeliveryTime = _referenceTime,
            CreatedAt = _referenceTime.AddMinutes(-2), // 2 mins ago (window is 5 mins)
            DeduplicationKey = key
        };
        await _repository.AddAsync(existing);

        var result = await _service.FindDuplicateAsync(key);

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result.Id);
    }

    [Fact]
    public async Task FindDuplicateAsync_OlderThanWindow_ReturnsNull()
    {
        var request = CreateSampleRequest();
        var key = _service.GenerateKey(request);

        var existing = new Notification
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId!,
            Type = NotificationType.Email,
            Recipient = request.Recipient!,
            Subject = request.Subject!,
            Message = request.Message!,
            Priority = NotificationPriority.High,
            RequestedDeliveryTime = _referenceTime,
            CreatedAt = _referenceTime.AddMinutes(-10), // 10 mins ago (window is 5 mins)
            DeduplicationKey = key
        };
        await _repository.AddAsync(existing);

        var result = await _service.FindDuplicateAsync(key);

        Assert.Null(result);
    }
}
