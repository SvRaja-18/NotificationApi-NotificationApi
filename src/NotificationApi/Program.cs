using System.Text.Json.Serialization;
using NotificationApi.BackgroundServices;
using NotificationApi.Configuration;
using NotificationApi.Providers;
using NotificationApi.Repositories;
using NotificationApi.Services;
using NotificationApi.Validation;

var builder = WebApplication.CreateBuilder(args);

// Options configuration
builder.Services.Configure<NotificationProcessingOptions>(
    builder.Configuration.GetSection(NotificationProcessingOptions.SectionName));

// Framework services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Core dependencies
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
builder.Services.AddSingleton<CreateNotificationRequestValidator>();
builder.Services.AddSingleton<INotificationDeduplicationService, NotificationDeduplicationService>();

// Provider implementations
builder.Services.AddSingleton<INotificationProvider, EmailNotificationProvider>();

// Processing & Application services
builder.Services.AddScoped<INotificationProcessor, NotificationProcessor>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Background processing worker
builder.Services.AddHostedService<NotificationBackgroundWorker>();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Required for WebApplicationFactory integration testing
public partial class Program { }
