namespace NotificationApi.Providers;

public class ProviderResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    private ProviderResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static ProviderResult Success() => new(true, null);

    public static ProviderResult Failure(string errorMessage) => new(false, errorMessage);
}
