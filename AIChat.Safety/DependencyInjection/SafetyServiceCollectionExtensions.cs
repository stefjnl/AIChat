using AIChat.Safety.Contracts;
using AIChat.Safety.Options;
using AIChat.Safety.Providers;
using AIChat.Safety.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using System.Net;

namespace AIChat.Safety.DependencyInjection;

/// <summary>
/// Extension methods for registering safety services in the dependency injection container.
/// </summary>
public static class SafetyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the AIChat safety services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAISafetyServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind safety options
        services.Configure<SafetyOptions>(configuration.GetSection("Safety"));

        // Register core safety services
        services.AddSafetyCore();

        return services;
    }

    /// <summary>
    /// Adds the AIChat safety services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure safety options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAISafetyServices(
        this IServiceCollection services,
        Action<SafetyOptions> configureOptions)
    {
        // Configure safety options
        services.Configure(configureOptions);

        // Register core safety services
        services.AddSafetyCore();

        return services;
    }

    /// <summary>
    /// Adds the core safety services with Azure Content Safety integration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddSafetyCore(this IServiceCollection services)
    {
        // Register OpenAI Moderation HTTP client with resilience
        services.AddHttpClient<OpenAIModerationEvaluator>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SafetyOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromMilliseconds(options.Resilience.TimeoutInMilliseconds);
        });

        // Register safety evaluator
        services.AddSingleton<ISafetyEvaluator, OpenAIModerationEvaluator>();

        // Register safety evaluation service
        services.AddSingleton<SafetyEvaluationService>();
        services.AddSingleton<ISafetyEvaluationService, SafetyEvaluationService>();

        // Add health check for safety service
        services.AddHealthChecks()
            .AddCheck<SafetyHealthCheck>("safety-service");

        return services;
    }

    /// <summary>
    /// Adds safety filtering services if enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSafetyFiltering(this IServiceCollection services)
    {
        // Register safety filter (implementation would depend on specific filtering strategy)
        // services.AddSingleton<ISafetyFilter, DefaultSafetyFilter>();

        return services;
    }

    /// <summary>
    /// Adds safety audit and monitoring services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSafetyAuditing(this IServiceCollection services)
    {
        // Register audit service (implementation would depend on audit storage strategy)
        // services.AddSingleton<ISafetyAuditService, DefaultSafetyAuditService>();

        return services;
    }

    /// <summary>
    /// Configures safety services for development environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDevelopmentSafety(this IServiceCollection services)
    {
        services.PostConfigure<SafetyOptions>(options =>
        {
            // Development-friendly defaults
            options.Enabled = true;
            options.FallbackBehavior = FallbackBehavior.FailOpen;
            options.Resilience.TimeoutInMilliseconds = 5000;
            options.Resilience.MaxRetries = 1;
            options.Audit.Enabled = false;
            options.RateLimit.Enabled = false;
        });

        return services;
    }

    /// <summary>
    /// Configures safety services for production environment.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProductionSafety(this IServiceCollection services)
    {
        services.PostConfigure<SafetyOptions>(options =>
        {
            // Production-safe defaults
            options.Enabled = true;
            options.FallbackBehavior = FallbackBehavior.FailClosed;
            options.Resilience.TimeoutInMilliseconds = 3000;
            options.Resilience.MaxRetries = 3;
            options.Audit.Enabled = true;
            options.Audit.LogFullContent = false;
            options.Audit.LogContentHashes = true;
            options.RateLimit.Enabled = true;
        });

        return services;
    }

    /// <summary>
    /// Creates a resilience policy for safety service HTTP calls.
    /// </summary>
    /// <returns>A resilience policy.</returns>
    private static IAsyncPolicy<HttpResponseMessage> GetResiliencePolicy()
    {
        var delay = Backoff.ExponentialBackoff(TimeSpan.FromMilliseconds(1000), retryCount: 3);

        var retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(response => response.StatusCode >= HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(delay);

        var circuitBreakerPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
    }

    /// <summary>
    /// Retrieves the API key from configuration or environment variables.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="options">The safety options.</param>
    /// <returns>The API key or null if not found.</returns>
    private static string? GetApiKeyFromConfiguration(IServiceProvider serviceProvider, SafetyOptions options)
    {
        var configuration = serviceProvider.GetService<IConfiguration>();
        
        // Try different sources for the API key
        return configuration?[$"Safety:{nameof(options.ApiKey)}"]
               ?? configuration?["OpenAI:ApiKey"]
               ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
               ?? Environment.GetEnvironmentVariable("MODERATION_API_KEY");
    }
}

/// <summary>
/// Interface for the safety evaluation service to support dependency injection.
/// </summary>
public interface ISafetyEvaluationService
{
    /// <summary>
    /// Evaluates user input for safety violations.
    /// </summary>
    Task<SafetyEvaluationResult> EvaluateUserInputAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates AI-generated output for safety violations.
    /// </summary>
    Task<SafetyEvaluationResult> EvaluateOutputAsync(string output, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates multiple messages in batch.
    /// </summary>
    Task<IReadOnlyList<SafetyEvaluationResult>> EvaluateBatchAsync(IEnumerable<string> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a streaming evaluator.
    /// </summary>
    IStreamingSafetyEvaluator CreateStreamingEvaluator();

    /// <summary>
    /// Filters and sanitizes text content.
    /// </summary>
    Task<FilteredTextResult?> FilterTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets safety status information.
    /// </summary>
    SafetyStatus GetSafetyStatus();
}

/// <summary>
/// Health check for the safety service.
/// </summary>
public class SafetyHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly ISafetyEvaluator _evaluator;
    private readonly ILogger<SafetyHealthCheck> _logger;

    public SafetyHealthCheck(ISafetyEvaluator evaluator, ILogger<SafetyHealthCheck> logger)
    {
        _evaluator = evaluator;
        _logger = logger;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform a simple safety check to verify the service is working
            var result = await _evaluator.EvaluateTextAsync("Hello world", cancellationToken);
            
            if (result != null)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    $"Safety service is operational. Provider: {_evaluator.GetProviderName()}");
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                "Safety service returned null result");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Safety service health check failed");
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Safety service is not operational", ex);
        }
    }
}