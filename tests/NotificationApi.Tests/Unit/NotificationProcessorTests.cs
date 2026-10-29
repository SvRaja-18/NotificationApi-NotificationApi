using Microsoft.Extensions.Logging.Abstractions;
using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;
using NotificationApi.Providers;
using NotificationApi.Repositories;
using NotificationApi.Services;
using NotificationApi.Tests.TestHelpers;
using Xunit;

namespace NotificationApi.Tests.Unit;

public class NotificationProcessorTests
{
    private readonly InMemoryNotificationRepository _repository = new();
    private readonly TestTimeProvider _timeProvider;
    private readonly DateTimeOffset _referenceTime = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    public NotificationProcessorTests()
    {
        _timeProvider = new TestTimeProvider(_referenceTime);
    }

    private Notification CreateNotification(
        string customerId,
        NotificationPriority priority,
        string recipient = "user@example.com",
        DateTimeOffset? requestedDeliveryTime = null)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Type = NotificationType.Email,
            Recipient = recipient,
            Subject = "Subject",
            Message = "Message",
            Priority = priority,
            RequestedDeliveryTime = requestedDeliveryTime ?? _referenceTime,
            CreatedAt = _referenceTime,
            Status = NotificationStatus.Pending
        };
    }

    [Fact]
    public async Task ProcessPendingAsync_ProcessesHighPriorityBeforeNormalAndLow()
    {
        var processedOrder = new List<Guid>();

        var mockProvider = new MockEmailProvider(onSend: n =>
        {
            processedOrder.Add(n.Id);
            return Task.FromResult(ProviderResult.Success());
        });

        var processor = new NotificationProcessor(
            _repository,
            new[] { mockProvider },
            NullLogger<NotificationProcessor>.Instance,
            _timeProvider);

        var low = CreateNotification("CUST-1", NotificationPriority.Low);
        var normal = CreateNotification("CUST-2", NotificationPriority.Normal);
        var high = CreateNotification("CUST-3", NotificationPriority.High);

        // Add out of priority order
        await _repository.AddAsync(low);
        await _repository.AddAsync(high);
        await _repository.AddAsync(normal);

        var result = await processor.ProcessPendingAsync();

        Assert.Equal(3, result.TotalEligible);
        Assert.Equal(3, result.Succeeded);
        Assert.Equal(0, result.Failed);

        Assert.Equal(3, processedOrder.Count);
        Assert.Equal(high.Id, processedOrder[0]);
        Assert.Equal(normal.Id, processedOrder[1]);
        Assert.Equal(low.Id, processedOrder[2]);

        // Verify entities updated to Sent
        var highEntity = await _repository.GetByIdAsync(high.Id);
        Assert.Equal(NotificationStatus.Sent, highEntity!.Status);
        Assert.NotNull(highEntity.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_ProviderFailure_UpdatesStatusToFailedWithoutCrashing()
    {
        var mockProvider = new MockEmailProvider(onSend: n =>
            Task.FromResult(ProviderResult.Failure("SMTP connection rejected")));

        var processor = new NotificationProcessor(
            _repository,
            new[] { mockProvider },
            NullLogger<NotificationProcessor>.Instance,
            _timeProvider);

        var notification = CreateNotification("CUST-1", NotificationPriority.Normal);
        await _repository.AddAsync(notification);

        var result = await processor.ProcessPendingAsync();

        Assert.Equal(1, result.TotalEligible);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.Failed);

        var entity = await _repository.GetByIdAsync(notification.Id);
        Assert.NotNull(entity);
        Assert.Equal(NotificationStatus.Failed, entity.Status);
        Assert.Equal("SMTP connection rejected", entity.FailureReason);
        Assert.NotNull(entity.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_ProviderThrowsUnexpectedException_HandlesWithoutCrashing()
    {
        var mockProvider = new MockEmailProvider(onSend: n =>
            throw new InvalidOperationException("Fatal network interface failure"));

        var processor = new NotificationProcessor(
            _repository,
            new[] { mockProvider },
            NullLogger<NotificationProcessor>.Instance,
            _timeProvider);

        var notification = CreateNotification("CUST-1", NotificationPriority.High);
        await _repository.AddAsync(notification);

        // Should complete without throwing exception
        var result = await processor.ProcessPendingAsync();

        Assert.Equal(1, result.TotalEligible);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.Failed);

        var entity = await _repository.GetByIdAsync(notification.Id);
        Assert.NotNull(entity);
        Assert.Equal(NotificationStatus.Failed, entity.Status);
        Assert.Contains("Fatal network interface failure", entity.FailureReason);
    }

    [Fact]
    public async Task ProcessPendingAsync_FutureDeliveryTime_SkippedUntilDue()
    {
        var mockProvider = new MockEmailProvider();
        var processor = new NotificationProcessor(
            _repository,
            new[] { mockProvider },
            NullLogger<NotificationProcessor>.Instance,
            _timeProvider);

        var futureNotification = CreateNotification(
            "CUST-1",
            NotificationPriority.High,
            requestedDeliveryTime: _referenceTime.AddHours(2));

        await _repository.AddAsync(futureNotification);

        var result = await processor.ProcessPendingAsync();

        Assert.Equal(0, result.TotalEligible);
        Assert.Equal(0, result.Succeeded);

        var entity = await _repository.GetByIdAsync(futureNotification.Id);
        Assert.Equal(NotificationStatus.Pending, entity!.Status);
    }

    [Fact]
    public async Task ProcessSingleAsync_NonExistentId_ReturnsNull()
    {
        var mockProvider = new MockEmailProvider();
        var processor = new NotificationProcessor(
            _repository,
            new[] { mockProvider },
            NullLogger<NotificationProcessor>.Instance,
            _timeProvider);

        var result = await processor.ProcessSingleAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    private class MockEmailProvider : INotificationProvider
    {
        private readonly Func<Notification, Task<ProviderResult>>? _onSend;

        public NotificationType SupportedType => NotificationType.Email;

        public MockEmailProvider(Func<Notification, Task<ProviderResult>>? onSend = null)
        {
            _onSend = onSend;
        }

        public Task<ProviderResult> SendAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            if (_onSend != null)
                return _onSend(notification);

            return Task.FromResult(ProviderResult.Success());
        }
    }
}
