using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;
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

// OpenAPI / Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Customer Notification Processing API",
        Version = "v1",
        Description = "A resilient .NET 10 microservice for validating, storing, deduplicating, and processing customer notifications with priority scheduling.",
        Contact = new OpenApiContact
        {
            Name = "Notification Service Engineering Team"
        }
    });

    // Include XML code comments in Swagger UI
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
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

// Swagger API Documentation & UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Processing API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
    c.EnableTryItOutByDefault();
});

// Root redirect to Swagger UI
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapControllers();

app.Run();

// Required for WebApplicationFactory integration testing
public partial class Program { }
