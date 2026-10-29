using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NotificationApi.DTOs;
using Xunit;

namespace NotificationApi.Tests.Integration;

public class NotificationsApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public NotificationsApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateNotification_ValidPayload_Returns201CreatedWithLocation()
    {
        var request = new CreateNotificationRequest
        {
            CustomerId = "CUST-API-01",
            Type = "Email",
            Recipient = "customer@example.com",
            Subject = "Order Confirmation",
            Message = "Your order #12345 has been placed.",
            Priority = "High",
            RequestedDeliveryTime = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var response = await _client.PostAsJsonAsync("/api/notifications", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("CUST-API-01", body.CustomerId);
        Assert.Equal("Pending", body.Status);
        Assert.False(body.IsDuplicate);
    }

    [Fact]
    public async Task CreateNotification_DuplicateRequest_Returns200OkWithDuplicateHeader()
    {
        var customerId = $"CUST-DUP-{Guid.NewGuid():N}";
        var deliveryTime = DateTimeOffset.UtcNow.AddMinutes(10);

        var request = new CreateNotificationRequest
        {
            CustomerId = customerId,
            Type = "Email",
            Recipient = "dup-test@example.com",
            Subject = "Duplicate Test",
            Message = "This message may be retried.",
            Priority = "Normal",
            RequestedDeliveryTime = deliveryTime
        };

        // First attempt -> 201 Created
        var response1 = await _client.PostAsJsonAsync("/api/notifications", request);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        var body1 = await response1.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(body1);

        // Immediate retry (identical payload within window) -> 200 OK
        var response2 = await _client.PostAsJsonAsync("/api/notifications", request);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.True(response2.Headers.Contains("X-Duplicate-Detected"));

        var body2 = await response2.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(body2);
        Assert.Equal(body1.Id, body2.Id); // Same notification returned
        Assert.True(body2.IsDuplicate);
    }

    [Fact]
    public async Task CreateNotification_InvalidPayload_Returns400BadRequestWithErrors()
    {
        var invalidRequest = new CreateNotificationRequest
        {
            CustomerId = "", // Empty CustomerId
            Type = "SMS", // Unsupported type
            Recipient = "not-an-email", // Invalid email
            Priority = "InvalidPriority", // Invalid priority
            RequestedDeliveryTime = DateTimeOffset.UtcNow.AddHours(-2) // Past time
        };

        var response = await _client.PostAsJsonAsync("/api/notifications", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("CustomerId", content);
        Assert.Contains("Type", content);
        Assert.Contains("Recipient", content);
        Assert.Contains("Priority", content);
        Assert.Contains("RequestedDeliveryTime", content);
    }

    [Fact]
    public async Task GetById_ExistingNotification_Returns200Ok()
    {
        var request = new CreateNotificationRequest
        {
            CustomerId = "CUST-GET-01",
            Type = "Email",
            Recipient = "get@example.com",
            Subject = "Test Get",
            Message = "Testing GetById",
            Priority = "Normal",
            RequestedDeliveryTime = DateTimeOffset.UtcNow
        };

        var createResponse = await _client.PostAsJsonAsync("/api/notifications", request);
        var created = await createResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/notifications/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.CustomerId, fetched.CustomerId);
    }

    [Fact]
    public async Task GetById_NonExistentNotification_Returns404NotFound()
    {
        var response = await _client.GetAsync($"/api/notifications/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByCustomer_ReturnsAllCustomerNotifications()
    {
        var customerId = $"CUST-LIST-{Guid.NewGuid():N}";

        for (int i = 1; i <= 3; i++)
        {
            await _client.PostAsJsonAsync("/api/notifications", new CreateNotificationRequest
            {
                CustomerId = customerId,
                Type = "Email",
                Recipient = "customer@example.com",
                Subject = $"Message {i}",
                Message = $"Body {i}",
                Priority = "Normal",
                RequestedDeliveryTime = DateTimeOffset.UtcNow
            });
        }

        var response = await _client.GetAsync($"/api/notifications/customer/{customerId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.All(list, n => Assert.Equal(customerId, n.CustomerId));
    }

    [Fact]
    public async Task ProcessPending_SuccessfullySendsAndUpdatesStatus()
    {
        var customerId = $"CUST-PROC-{Guid.NewGuid():N}";

        var createResponse = await _client.PostAsJsonAsync("/api/notifications", new CreateNotificationRequest
        {
            CustomerId = customerId,
            Type = "Email",
            Recipient = "valid-send@example.com",
            Subject = "Delivery Test",
            Message = "Testing processing endpoint",
            Priority = "High",
            RequestedDeliveryTime = DateTimeOffset.UtcNow
        });

        var created = await createResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(created);

        // Trigger manual processing
        var procResponse = await _client.PostAsync("/api/notifications/process", null);
        Assert.Equal(HttpStatusCode.OK, procResponse.StatusCode);

        // Verify status changed to Sent
        var checkResponse = await _client.GetAsync($"/api/notifications/{created.Id}");
        var updated = await checkResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Sent", updated.Status);
        Assert.NotNull(updated.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPending_SimulatedFailure_UpdatesStatusToFailedWithoutCrashing()
    {
        var customerId = $"CUST-FAIL-{Guid.NewGuid():N}";

        var createResponse = await _client.PostAsJsonAsync("/api/notifications", new CreateNotificationRequest
        {
            CustomerId = customerId,
            Type = "Email",
            Recipient = "fail@example.com", // Triggers simulated failure
            Subject = "Failure Test",
            Message = "This email delivery will simulate a failure.",
            Priority = "Normal",
            RequestedDeliveryTime = DateTimeOffset.UtcNow
        });

        var created = await createResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(created);

        // Trigger processing
        var procResponse = await _client.PostAsync("/api/notifications/process", null);
        Assert.Equal(HttpStatusCode.OK, procResponse.StatusCode);

        // Verify status changed to Failed with explanation
        var checkResponse = await _client.GetAsync($"/api/notifications/{created.Id}");
        var updated = await checkResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Failed", updated.Status);
        Assert.NotNull(updated.FailureReason);
        Assert.Contains("simulated failure", updated.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAll_WithFilters_ReturnsMatchingNotifications()
    {
        var customerId = $"CUST-FILTER-{Guid.NewGuid():N}";

        var createResponse = await _client.PostAsJsonAsync("/api/notifications", new CreateNotificationRequest
        {
            CustomerId = customerId,
            Type = "Email",
            Recipient = "filter@example.com",
            Subject = "Filter Test",
            Message = "Filter test content",
            Priority = "High",
            RequestedDeliveryTime = DateTimeOffset.UtcNow
        });
        var created = await createResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(created);

        // Filter by CustomerId
        var response = await _client.GetAsync($"/api/notifications?customerId={customerId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        Assert.NotNull(list);
        Assert.Contains(list, n => n.Id == created.Id);

        // Filter by non-matching customer
        var emptyResponse = await _client.GetAsync($"/api/notifications?customerId=NONEXISTENT-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        var emptyList = await emptyResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        Assert.NotNull(emptyList);
        Assert.Empty(emptyList);
    }

    [Fact]
    public async Task CreateNotification_WithExplicitIdempotencyKeyHeader_Returns200OnDuplicate()
    {
        var idempotencyKey = $"IDEMP-{Guid.NewGuid():N}";
        var customerId = $"CUST-IDEMP-{Guid.NewGuid():N}";

        var requestBody = new CreateNotificationRequest
        {
            CustomerId = customerId,
            Type = "Email",
            Recipient = "idemp@example.com",
            Subject = "Idempotency Test",
            Message = "Testing explicit header",
            Priority = "Normal",
            RequestedDeliveryTime = DateTimeOffset.UtcNow
        };

        // First request with Idempotency-Key header
        var msg1 = new HttpRequestMessage(HttpMethod.Post, "/api/notifications")
        {
            Content = JsonContent.Create(requestBody)
        };
        msg1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await _client.SendAsync(msg1);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        // Second request with same Idempotency-Key header
        var msg2 = new HttpRequestMessage(HttpMethod.Post, "/api/notifications")
        {
            Content = JsonContent.Create(requestBody)
        };
        msg2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await _client.SendAsync(msg2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.True(response2.Headers.Contains("X-Duplicate-Detected"));
    }

    [Fact]
    public async Task ProcessSingle_ValidPendingNotification_Returns200OkAndProcessesNotification()
    {
        var customerId = $"CUST-SINGLE-{Guid.NewGuid():N}";

        var createResponse = await _client.PostAsJsonAsync("/api/notifications", new CreateNotificationRequest
        {
            CustomerId = customerId,
            Type = "Email",
            Recipient = "single@example.com",
            Subject = "Single Test",
            Message = "Processing single notification",
            Priority = "High",
            RequestedDeliveryTime = DateTimeOffset.UtcNow
        });
        var created = await createResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(created);

        // Process this specific notification
        var procResponse = await _client.PostAsync($"/api/notifications/{created.Id}/process", null);
        Assert.Equal(HttpStatusCode.OK, procResponse.StatusCode);

        var result = await procResponse.Content.ReadFromJsonAsync<NotificationProcessItemResult>();
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.True(result.Success);

        // Verify status is now Sent
        var checkResponse = await _client.GetAsync($"/api/notifications/{created.Id}");
        var updated = await checkResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Sent", updated.Status);
    }

    [Fact]
    public async Task ProcessSingle_NonExistentNotification_Returns404NotFound()
    {
        var randomId = Guid.NewGuid();
        var procResponse = await _client.PostAsync($"/api/notifications/{randomId}/process", null);
        Assert.Equal(HttpStatusCode.NotFound, procResponse.StatusCode);
    }
}

