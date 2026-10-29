using System.Net.Mail;
using NotificationApi.Domain.Enums;
using NotificationApi.DTOs;

namespace NotificationApi.Validation;

public class CreateNotificationRequestValidator
{
    private readonly TimeProvider _timeProvider;
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromSeconds(5);

    public CreateNotificationRequestValidator(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ValidationResult Validate(CreateNotificationRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void AddError(string field, string error)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                list = new List<string>();
                errors[field] = list;
            }
            list.Add(error);
        }

        // 1. CustomerId
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            AddError(nameof(request.CustomerId), "Customer ID is required.");
        }

        // 2. Notification type
        bool isEmail = false;
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            AddError(nameof(request.Type), "Notification type is required.");
        }
        else if (Enum.TryParse<NotificationType>(request.Type, ignoreCase: true, out var parsedType) &&
                 Enum.IsDefined(typeof(NotificationType), parsedType))
        {
            isEmail = parsedType == NotificationType.Email;
        }
        else
        {
            AddError(nameof(request.Type), $"Notification type '{request.Type}' is invalid. Currently, only 'Email' is supported.");
        }

        // 3. Recipient
        if (string.IsNullOrWhiteSpace(request.Recipient))
        {
            AddError(nameof(request.Recipient), "Recipient is required.");
        }
        else if (!IsValidEmail(request.Recipient))
        {
            AddError(nameof(request.Recipient), "Email recipient must contain a valid email address.");
        }

        // 4. Message
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            AddError(nameof(request.Message), "Message is required.");
        }

        // 5. Priority
        if (string.IsNullOrWhiteSpace(request.Priority))
        {
            AddError(nameof(request.Priority), "Priority is required. Allowed values are: Low, Normal, High.");
        }
        else if (!Enum.TryParse<NotificationPriority>(request.Priority, ignoreCase: true, out var parsedPriority) ||
                 !Enum.IsDefined(typeof(NotificationPriority), parsedPriority))
        {
            AddError(nameof(request.Priority), "Priority can only be: Low, Normal, High.");
        }

        // 6. RequestedDeliveryTime
        if (request.RequestedDeliveryTime.HasValue)
        {
            var nowUtc = _timeProvider.GetUtcNow();
            // Allow exact current time (or within small clock skew tolerance)
            if (request.RequestedDeliveryTime.Value < nowUtc - ClockSkewTolerance)
            {
                AddError(nameof(request.RequestedDeliveryTime), "Requested delivery time cannot be in the past.");
            }
        }

        var resultDict = errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
        return new ValidationResult(resultDict);
    }

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim();
        if (email.EndsWith("."))
            return false;

        try
        {
            var addr = new MailAddress(email);
            if (addr.Address != email)
                return false;

            // Host must contain at least one dot and not begin/end with dot
            var hostParts = addr.Host.Split('.');
            if (hostParts.Length < 2)
                return false;

            if (hostParts.Any(string.IsNullOrWhiteSpace))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
