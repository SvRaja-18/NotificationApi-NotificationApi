namespace NotificationApi.Validation;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IDictionary<string, string[]> Errors { get; }

    public ValidationResult()
    {
        Errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    public ValidationResult(IDictionary<string, string[]> errors)
    {
        Errors = new Dictionary<string, string[]>(errors, StringComparer.OrdinalIgnoreCase);
    }

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
