namespace NotificationApi.DTOs;

public class ProcessNotificationsResult
{
    public int TotalEligible { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<NotificationProcessItemResult> Items { get; init; } = Array.Empty<NotificationProcessItemResult>();
}

public class NotificationProcessItemResult
{
    public Guid Id { get; init; }
    public string Recipient { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
