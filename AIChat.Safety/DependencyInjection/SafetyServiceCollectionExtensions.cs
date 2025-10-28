using AIChat.Safety.Contracts;
using AIChat.Safety.Options;
using AIChat.Safety.Providers;
using AIChat.Safety.Services;
using AIChat.Safety.Middleware;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        // Bind safety options with configuration
        services.AddOptions<SafetyOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                config.GetSection("Safety").Bind(options);
                options.SetConfiguration(config);
            });

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
    /// Adds the core safety services with proper HttpClient configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddSafetyCore(this IServiceCollection services)
    {
        // Register OpenAI Moderation HTTP client with proper typed client pattern and resilience
        services.AddHttpClient<ISafetyEvaluator, OpenAIModerationEvaluator>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<SafetyOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromMilliseconds(options.Resilience.TimeoutInMilliseconds);
        })
        .AddStandardResilienceHandler(); // Use standard resilience with default settings

        // Register safety evaluation service
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
    /// Wraps an IChatClient with safety middleware for content moderation.
    /// </summary>
    /// <param name="innerClient">The inner chat client to wrap.</param>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <returns>A chat client wrapped with safety middleware.</returns>
    public static IChatClient UseSafetyMiddleware(
  this IChatClient innerClient,
        IServiceProvider serviceProvider)
    {
     if (innerClient == null) throw new ArgumentNullException(nameof(innerClient));
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        var safetyService = serviceProvider.GetRequiredService<ISafetyEvaluationService>();
        var logger = serviceProvider.GetRequiredService<ILogger<SafetyChatClientMiddleware>>();

        return new SafetyChatClientMiddleware(innerClient, safetyService, logger);
    }

    /// <summary>
    /// Configures a chat client builder to use safety middleware.
    /// </summary>
    /// <param name="builder">The chat client builder.</param>
    /// <returns>The chat client builder for chaining.</returns>
    public static IChatClient UseSafetyMiddleware(
        this IChatClient client,
      ISafetyEvaluationService safetyService,
        ILogger<SafetyChatClientMiddleware> logger)
    {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (safetyService == null) throw new ArgumentNullException(nameof(safetyService));
        if (logger == null) throw new ArgumentNullException(nameof(logger));

    return new SafetyChatClientMiddleware(client, safetyService, logger);
 }
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