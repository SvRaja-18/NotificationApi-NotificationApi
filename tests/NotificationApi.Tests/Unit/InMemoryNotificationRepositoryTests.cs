using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;
using NotificationApi.Repositories;
using Xunit;

namespace NotificationApi.Tests.Unit;

public class InMemoryNotificationRepositoryTests
{
    private readonly InMemoryNotificationRepository _repository = new();
    private readonly DateTimeOffset _referenceTime = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private Notification CreateNotification(
        string customerId,
        NotificationPriority priority,
        DateTimeOffset requestedDeliveryTime,
        DateTimeOffset createdAt)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Type = NotificationType.Email,
            Recipient = $"{customerId.ToLowerInvariant()}@example.com",
            Subject = "Subject",
            Message = "Message",
            Priority = priority,
            RequestedDeliveryTime = requestedDeliveryTime,
            CreatedAt = createdAt,
            Status = NotificationStatus.Pending
        };
    }

    [Fact]
    public async Task GetPendingEligibleAsync_OrdersHighPriorityBeforeNormalAndLow()
    {
        var low = CreateNotification("CUST-1", NotificationPriority.Low, _referenceTime, _referenceTime.AddSeconds(1));
        var normal = CreateNotification("CUST-2", NotificationPriority.Normal, _referenceTime, _referenceTime.AddSeconds(2));
        var high = CreateNotification("CUST-3", NotificationPriority.High, _referenceTime, _referenceTime.AddSeconds(3));

        // Add out of priority order
        await _repository.AddAsync(low);
        await _repository.AddAsync(normal);
        await _repository.AddAsync(high);

        var eligible = await _repository.GetPendingEligibleAsync(_referenceTime.AddMinutes(1));

        Assert.Equal(3, eligible.Count);
        Assert.Equal(NotificationPriority.High, eligible[0].Priority);
        Assert.Equal(NotificationPriority.Normal, eligible[1].Priority);
        Assert.Equal(NotificationPriority.Low, eligible[2].Priority);
    }

    [Fact]
    public async Task GetPendingEligibleAsync_SamePriority_PreservesFifoByRequestedDeliveryAndCreation()
    {
        // Edge case: Multiple notifications having the same priority
        var first = CreateNotification("CUST-1", NotificationPriority.High, _referenceTime, _referenceTime.AddSeconds(1));
        var second = CreateNotification("CUST-2", NotificationPriority.High, _referenceTime, _referenceTime.AddSeconds(5));
        var laterDelivery = CreateNotification("CUST-3", NotificationPriority.High, _referenceTime.AddMinutes(2), _referenceTime);

        await _repository.AddAsync(second);
        await _repository.AddAsync(first);
        await _repository.AddAsync(laterDelivery);

        var eligible = await _repository.GetPendingEligibleAsync(_referenceTime.AddMinutes(5));

        Assert.Equal(3, eligible.Count);
        Assert.Equal(first.Id, eligible[0].Id);
        Assert.Equal(second.Id, eligible[1].Id);
        Assert.Equal(laterDelivery.Id, eligible[2].Id);
    }

    [Fact]
    public async Task GetPendingEligibleAsync_FutureDeliveryTime_NotEligibleUntilDeliveryTime()
    {
        var future = CreateNotification("CUST-1", NotificationPriority.High, _referenceTime.AddMinutes(10), _referenceTime);
        await _repository.AddAsync(future);

        // Check before delivery time
        var beforeTime = await _repository.GetPendingEligibleAsync(_referenceTime.AddMinutes(5));
        Assert.Empty(beforeTime);

        // Check at delivery time
        var atTime = await _repository.GetPendingEligibleAsync(_referenceTime.AddMinutes(10));
        Assert.Single(atTime);
        Assert.Equal(future.Id, atTime[0].Id);
    }

    [Fact]
    public async Task TryClaimForProcessingAsync_AtomicallyChangesPendingToProcessing()
    {
        var notification = CreateNotification("CUST-1", NotificationPriority.Normal, _referenceTime, _referenceTime);
        await _repository.AddAsync(notification);

        // First claim succeeds
        var firstClaim = await _repository.TryClaimForProcessingAsync(notification.Id);
        Assert.True(firstClaim);

        var updated = await _repository.GetByIdAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.Equal(NotificationStatus.Processing, updated.Status);
        Assert.Equal(1, updated.AttemptCount);

        // Second claim fails (prevents concurrent double processing)
        var secondClaim = await _repository.TryClaimForProcessingAsync(notification.Id);
        Assert.False(secondClaim);
    }

    [Fact]
    public async Task MarkCompletedAsync_UpdatesStatusAndFailureReason()
    {
        var notification = CreateNotification("CUST-1", NotificationPriority.Normal, _referenceTime, _referenceTime);
        await _repository.AddAsync(notification);

        await _repository.TryClaimForProcessingAsync(notification.Id);

        var processedAt = _referenceTime.AddSeconds(10);
        await _repository.MarkCompletedAsync(notification.Id, NotificationStatus.Failed, processedAt, "Provider connection timeout");

        var updated = await _repository.GetByIdAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.Equal(NotificationStatus.Failed, updated.Status);
        Assert.Equal(processedAt, updated.ProcessedAt);
        Assert.Equal("Provider connection timeout", updated.FailureReason);
    }

    [Fact]
    public async Task GetByCustomerIdAsync_ReturnsCustomerNotificationsSortedDescending()
    {
        var n1 = CreateNotification("CUST-100", NotificationPriority.Normal, _referenceTime, _referenceTime.AddMinutes(1));
        var n2 = CreateNotification("CUST-100", NotificationPriority.High, _referenceTime, _referenceTime.AddMinutes(2));
        var nOther = CreateNotification("CUST-999", NotificationPriority.Low, _referenceTime, _referenceTime);

        await _repository.AddAsync(n1);
        await _repository.AddAsync(n2);
        await _repository.AddAsync(nOther);

        var results = await _repository.GetByCustomerIdAsync("CUST-100");

        Assert.Equal(2, results.Count);
        Assert.Equal(n2.Id, results[0].Id); // Newest first
        Assert.Equal(n1.Id, results[1].Id);
    }

    [Fact]
    public async Task GetByCustomerIdAsync_NonExistentCustomer_ReturnsEmptyList()
    {
        var results = await _repository.GetByCustomerIdAsync("NON_EXISTENT");
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }
}
