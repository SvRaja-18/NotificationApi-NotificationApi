using NotificationApi.DTOs;
using NotificationApi.Tests.TestHelpers;
using NotificationApi.Validation;
using Xunit;

namespace NotificationApi.Tests.Unit;

public class CreateNotificationRequestValidatorTests
{
    private readonly TestTimeProvider _fakeTimeProvider;
    private readonly CreateNotificationRequestValidator _validator;
    private readonly DateTimeOffset _referenceTime = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    public CreateNotificationRequestValidatorTests()
    {
        _fakeTimeProvider = new TestTimeProvider(_referenceTime);
        _validator = new CreateNotificationRequestValidator(_fakeTimeProvider);
    }

    private CreateNotificationRequest CreateValidRequest() => new()
    {
        CustomerId = "CUST-101",
        Type = "Email",
        Recipient = "customer@example.com",
        Subject = "Order Confirmed",
        Message = "Your order has been confirmed.",
        Priority = "Normal",
        RequestedDeliveryTime = _referenceTime.AddMinutes(10)
    };

    [Fact]
    public void Validate_ValidRequest_ReturnsSuccess()
    {
        var request = CreateValidRequest();
        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingCustomerId_Fails(string? customerId)
    {
        var request = CreateValidRequest();
        request.CustomerId = customerId;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(request.CustomerId), result.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingType_Fails(string? type)
    {
        var request = CreateValidRequest();
        request.Type = type;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(request.Type), result.Errors.Keys);
    }

    [Theory]
    [InlineData("SMS")]
    [InlineData("Push")]
    [InlineData("CarrierPigeon")]
    public void Validate_UnsupportedType_Fails(string type)
    {
        var request = CreateValidRequest();
        request.Type = type;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains("Currently, only 'Email' is supported", result.Errors[nameof(request.Type)].First());
    }

    [Theory]
    [InlineData("email")]
    [InlineData("EMAIL")]
    [InlineData("Email")]
    public void Validate_EmailTypeCaseInsensitive_Passes(string type)
    {
        var request = CreateValidRequest();
        request.Type = type;

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingRecipient_Fails(string? recipient)
    {
        var request = CreateValidRequest();
        request.Recipient = recipient;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(request.Recipient), result.Errors.Keys);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@missinguser.com")]
    [InlineData("user@")]
    [InlineData("user@.com")]
    [InlineData("user name@domain.com")]
    [InlineData("user@domain")]
    public void Validate_InvalidEmailFormat_Fails(string invalidEmail)
    {
        var request = CreateValidRequest();
        request.Recipient = invalidEmail;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(request.Recipient), result.Errors.Keys);
        Assert.Contains("valid email address", result.Errors[nameof(request.Recipient)].First());
    }

    [Theory]
    [InlineData("customer@example.com")]
    [InlineData("first.last@company.co.uk")]
    [InlineData("user+tag@sub.domain.org")]
    public void Validate_ValidEmailFormat_Passes(string validEmail)
    {
        var request = CreateValidRequest();
        request.Recipient = validEmail;

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingMessage_Fails(string? message)
    {
        var request = CreateValidRequest();
        request.Message = message;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(request.Message), result.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Urgent")]
    [InlineData("Critical")]
    public void Validate_InvalidPriority_Fails(string? priority)
    {
        var request = CreateValidRequest();
        request.Priority = priority;

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(request.Priority), result.Errors.Keys);
    }

    [Theory]
    [InlineData("low")]
    [InlineData("NORMAL")]
    [InlineData("High")]
    public void Validate_ValidPriorityCaseInsensitive_Passes(string priority)
    {
        var request = CreateValidRequest();
        request.Priority = priority;

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RequestedDeliveryTimeInPast_Fails()
    {
        var request = CreateValidRequest();
        // 1 hour in the past
        request.RequestedDeliveryTime = _referenceTime.AddHours(-1);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(request.RequestedDeliveryTime), result.Errors.Keys);
        Assert.Contains("cannot be in the past", result.Errors[nameof(request.RequestedDeliveryTime)].First());
    }

    [Fact]
    public void Validate_RequestedDeliveryTimeExactlyEqualToCurrentTime_Passes()
    {
        // Edge case explicitly highlighted in exercise specification
        var request = CreateValidRequest();
        request.RequestedDeliveryTime = _referenceTime;

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RequestedDeliveryTimeNull_Passes()
    {
        var request = CreateValidRequest();
        request.RequestedDeliveryTime = null;

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
