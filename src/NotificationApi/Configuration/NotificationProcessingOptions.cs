namespace NotificationApi.Configuration;

public class NotificationProcessingOptions
{
    public const string SectionName = "NotificationProcessing";

    /// <summary>
    /// The window of time within which identical requests are considered duplicates.
    /// </summary>
    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether the background worker service should poll and process pending notifications automatically.
    /// </summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>
    /// Interval between background polling cycles.
    /// </summary>
    public TimeSpan WorkerInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Simulated provider failure rate or test flag.
    /// </summary>
    public bool SimulateProviderFailures { get; set; } = false;
}
