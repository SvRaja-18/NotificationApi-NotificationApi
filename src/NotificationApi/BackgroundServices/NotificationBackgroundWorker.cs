using Microsoft.Extensions.Options;
using NotificationApi.Configuration;
using NotificationApi.Services;

namespace NotificationApi.BackgroundServices;

public class NotificationBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<NotificationProcessingOptions> _options;
    private readonly ILogger<NotificationBackgroundWorker> _logger;

    public NotificationBackgroundWorker(
        IServiceProvider serviceProvider,
        IOptionsMonitor<NotificationProcessingOptions> options,
        ILogger<NotificationBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;

            if (options.WorkerEnabled)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<INotificationProcessor>();

                    var result = await processor.ProcessPendingAsync(stoppingToken);
                    if (result.TotalEligible > 0)
                    {
                        _logger.LogInformation(
                            "Background cycle processed {Total} notification(s): {Succeeded} succeeded, {Failed} failed",
                            result.TotalEligible,
                            result.Succeeded,
                            result.Failed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error occurred during background notification processing cycle");
                }
            }

            try
            {
                await Task.Delay(options.WorkerInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Notification Background Worker stopped.");
    }
}
