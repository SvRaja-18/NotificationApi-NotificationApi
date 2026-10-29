using Microsoft.Extensions.Options;
using NotificationApi.Configuration;
using NotificationApi.Domain.Enums;
using NotificationApi.Domain.Models;

namespace NotificationApi.Providers;

public class EmailNotificationProvider : INotificationProvider
{
    private readonly ILogger<EmailNotificationProvider> _logger;
    private readonly IOptionsMonitor<NotificationProcessingOptions> _options;

    public NotificationType SupportedType => NotificationType.Email;

    public EmailNotificationProvider(
        ILogger<EmailNotificationProvider> logger,
        IOptionsMonitor<NotificationProcessingOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    public async Task<ProviderResult> SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simulate minimal asynchronous I/O delay (e.g. network roundtrip)
            await Task.Delay(10, cancellationToken);

            // Deterministic simulation hooks for testing failure scenarios:
            // 1. Configured globally in options
            // 2. Triggered by specific test email prefixes (e.g., fail@, error@)
            if (_options.CurrentValue.SimulateProviderFailures ||
                notification.Recipient.StartsWith("fail", StringComparison.OrdinalIgnoreCase) ||
                notification.Recipient.StartsWith("error", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Simulated provider failure while dispatching notification {Id} to {Recipient}",
                    notification.Id,
                    notification.Recipient);

                return ProviderResult.Failure("Email provider unavailable: SMTP relay rejected connection (simulated failure).");
            }

            _logger.LogInformation(
                "Simulated email sent successfully. ID: {Id}, Recipient: {Recipient}, Priority: {Priority}, Subject: '{Subject}'",
                notification.Id,
                notification.Recipient,
                notification.Priority,
                notification.Subject);

            return ProviderResult.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred in email provider for notification {Id}", notification.Id);
            return ProviderResult.Failure($"Unexpected provider error: {ex.Message}");
        }
    }
}
