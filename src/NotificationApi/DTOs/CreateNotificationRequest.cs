namespace NotificationApi.DTOs;

public class CreateNotificationRequest
{
    public string? CustomerId { get; set; }
    public string? Type { get; set; }
    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }
    public string? Priority { get; set; }
    public DateTimeOffset? RequestedDeliveryTime { get; set; }
}
