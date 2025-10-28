using AIChat.Safety.Contracts;
using Microsoft.Extensions.Configuration;

namespace AIChat.Safety.Options;

/// <summary>
/// Configuration options for safety evaluation system.
/// Controls how content safety is evaluated and handled across the application.
/// </summary>
public class SafetyOptions
{
    private IConfiguration? _configuration;

    /// <summary>
    /// Gets or sets whether safety evaluation is enabled.
    /// </summary>
  public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets OpenAI API endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = "https://api.openai.com/v1/moderations";

    /// <summary>
    /// Gets or sets OpenAI API key directly (for development/testing scenarios).
    /// Note: This property is deprecated. Use GetApiKey() method to retrieve API key from configuration.
    /// </summary>
    [Obsolete("Use GetApiKey() method to retrieve API key from configuration instead.")]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Sets the configuration instance for API key resolution.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    public void SetConfiguration(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Gets the OpenAI API key from configuration or user secrets.
    /// Checks multiple sources in order of precedence:
    /// 1. Safety:ApiKey (for backward compatibility)
    /// 2. Safety:OpenAI:ApiKey (nested configuration)
    /// 3. OpenAI:ApiKey (standard OpenAI configuration path)
    /// 4. OPENAI_API_KEY environment variable
    /// </summary>
    /// <returns>The API key or null if not found.</returns>
    public string? GetApiKey()
    {
        if (_configuration == null)
        {
#pragma warning disable CS0618 // Type or member is obsolete
     return ApiKey; // Fallback to the direct property for backward compatibility
#pragma warning restore CS0618
    }

        // Try different sources for the API key in order of precedence
        return _configuration["Safety:ApiKey"]
           ?? _configuration["Safety:OpenAI:ApiKey"]
?? _configuration["OpenAI:ApiKey"]
       ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
#pragma warning disable CS0618
        ?? ApiKey; // Fallback to the direct property for backward compatibility
#pragma warning restore CS0618
    }

  /// <summary>
    /// Gets or sets OpenAI organization ID (optional).
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets OpenAI model to use for moderation (defaults to "omni-moderation-latest").
    /// </summary>
    public string Model { get; set; } = "omni-moderation-latest";

    /// <summary>
    /// Gets or sets whether to use legacy Azure Content Safety endpoint (for backward compatibility).
    /// </summary>
    [Obsolete("Use OpenAI Moderation API instead")]
    public bool UseLegacyAzure { get; set; } = false;

    /// <summary>
    /// Gets or sets behavior to use when safety service is unavailable.
    /// </summary>
    public FallbackBehavior FallbackBehavior { get; set; } = FallbackBehavior.FailOpen;

    /// <summary>
    /// Gets or sets policy for evaluating user input content.
    /// </summary>
    public PolicySettings InputPolicy { get; set; } = new();

    /// <summary>
    /// Gets or sets policy for evaluating AI-generated output content.
    /// </summary>
    public PolicySettings OutputPolicy { get; set; } = new();

    /// <summary>
    /// Gets or sets resilience and retry configuration for safety service calls.
    /// </summary>
    public ResilienceSettings Resilience { get; set; } = new();

    /// <summary>
    /// Gets or sets configuration for content filtering and sanitization.
    /// </summary>
    public FilteringSettings Filtering { get; set; } = new();

    /// <summary>
    /// Gets or sets configuration for audit logging and monitoring.
    /// </summary>
    public AuditSettings Audit { get; set; } = new();

    /// <summary>
    /// Gets or sets configuration for rate limiting safety evaluations.
    /// </summary>
    public RateLimitSettings RateLimit { get; set; } = new();
}

/// <summary>
/// Defines policy settings for specific content types (input/output).
/// </summary>
public class PolicySettings
{
    /// <summary>
    /// Gets or sets minimum severity level to trigger a violation for each harm category.
    /// Azure Content Safety uses levels 0, 2, 4, 6 where higher is more severe.
    /// </summary>
    public Dictionary<HarmCategory, int> Thresholds { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to block content immediately upon violation detection.
    /// </summary>
    public bool BlockOnViolation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to require multiple categories to be violated before blocking.
    /// </summary>
    public bool RequireMultipleCategories { get; set; } = false;

    /// <summary>
    /// Gets or sets minimum number of categories that must be violated if RequireMultipleCategories is true.
    /// </summary>
    public int MinimumCategoryViolations { get; set; } = 2;

    /// <summary>
    /// Gets or sets maximum risk score allowed before content is blocked (0-100).
    /// </summary>
    public int MaxRiskScore { get; set; } = 70;

    /// <summary>
    /// Gets or sets custom actions to take for specific harm categories.
    /// </summary>
    public Dictionary<HarmCategory, HarmAction> CategoryActions { get; set; } = new();
}

/// <summary>
/// Defines resilience and retry settings for safety service calls.
/// </summary>
public class ResilienceSettings
{
    /// <summary>
    /// Gets or sets timeout in milliseconds for safety evaluation requests.
    /// </summary>
    public int TimeoutInMilliseconds { get; set; } = 3000;

    /// <summary>
    /// Gets or sets maximum number of retry attempts for failed requests.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Gets or sets delay between retry attempts in milliseconds.
    /// </summary>
    public int RetryDelayInMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets number of consecutive failures before circuit breaker opens.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets duration in seconds that circuit breaker remains open.
    /// </summary>
    public int CircuitBreakerDurationInSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to use exponential backoff for retries.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets maximum backoff multiplier for exponential retry.
    /// </summary>
    public double MaxBackoffMultiplier { get; set; } = 8.0;
}

/// <summary>
/// Configuration for content filtering and sanitization.
/// </summary>
public class FilteringSettings
{
    /// <summary>
    /// Gets or sets whether content filtering is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the default filtering action to apply.
    /// </summary>
    public FilterActionType DefaultAction { get; set; } = FilterActionType.Mask;

    /// <summary>
    /// Gets or sets the character to use for masking content.
    /// </summary>
    public string MaskCharacter { get; set; } = "*";

    /// <summary>
    /// Gets or sets custom replacement text for redaction.
    /// </summary>
    public string RedactionText { get; set; } = "[REDACTED]";

    /// <summary>
    /// Gets or sets specific filtering actions per harm category.
    /// </summary>
    public Dictionary<HarmCategory, FilterActionType> CategoryActions { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to preserve original text length after filtering.
    /// </summary>
    public bool PreserveLength { get; set; } = true;
}

/// <summary>
/// Configuration for audit logging and monitoring.
/// </summary>
public class AuditSettings
{
    /// <summary>
    /// Gets or sets whether audit logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log full content in audit entries.
    /// </summary>
    public bool LogFullContent { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to log content hashes for integrity verification.
    /// </summary>
    public bool LogContentHashes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log evaluation metadata.
    /// </summary>
    public bool LogMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets retention period in days for audit logs.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets minimum severity level to trigger audit alerts.
    /// </summary>
    public int AlertThreshold { get; set; } = 4;

    /// <summary>
    /// Gets or sets webhook URL for audit alerts.
    /// </summary>
    public string? AlertWebhookUrl { get; set; }
}

/// <summary>
/// Configuration for rate limiting safety evaluations.
/// </summary>
public class RateLimitSettings
{
    /// <summary>
    /// Gets or sets whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets maximum number of evaluations allowed per window.
    /// </summary>
    public int MaxEvaluationsPerWindow { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the time window in seconds for rate limiting.
    /// </summary>
    public int WindowInSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether to apply rate limiting per user/session.
    /// </summary>
    public bool PerUserLimiting { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to apply rate limiting per IP address.
    /// </summary>
    public bool PerIPLimiting { get; set; } = false;

    /// <summary>
    /// Gets or sets action to take when rate limit is exceeded.
    /// </summary>
    public RateLimitAction ExceededAction { get; set; } = RateLimitAction.Throttle;
}

/// <summary>
/// Defines the behavior when safety service is unavailable.
/// </summary>
public enum FallbackBehavior
{
    /// <summary>
    /// Allow content to proceed when safety service fails (less safe).
    /// </summary>
    FailOpen,

    /// <summary>
    /// Block content when safety service fails (more safe).
    /// </summary>
    FailClosed
}

/// <summary>
/// Defines actions to take for specific harm categories.
/// </summary>
public enum HarmAction
{
    /// <summary>
    /// Block the content completely.
    /// </summary>
    Block,

    /// <summary>
    /// Filter the content to remove harmful parts.
    /// </summary>
    Filter,

    /// <summary>
    /// Flag the content but allow it to proceed.
    /// </summary>
    Flag,

    /// <summary>
    /// Require human review before proceeding.
    /// </summary>
    RequireReview,

    /// <summary>
    /// Log the violation but take no action.
    /// </summary>
    LogOnly
}

/// <summary>
/// Defines actions when rate limits are exceeded.
/// </summary>
public enum RateLimitAction
{
    /// <summary>
    /// Reject the request immediately.
    /// </summary>
    Reject,

    /// <summary>
    /// Slow down the response.
    /// </summary>
    Throttle,

    /// <summary>
    /// Queue the request for later processing.
    /// </summary>
    Queue,

    /// <summary>
    /// Allow the request but log the violation.
    /// </summary>
    LogOnly
}