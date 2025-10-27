using AIChat.Safety.Contracts;
using AIChat.Safety.DependencyInjection;
using AIChat.Safety.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AIChat.Safety.Services;

/// <summary>
/// Provides comprehensive safety evaluation services for chat applications.
/// Coordinates between different safety evaluators and provides high-level safety operations.
/// </summary>
public class SafetyEvaluationService : ISafetyEvaluationService
{
    private readonly ISafetyEvaluator _evaluator;
    private readonly ISafetyFilter? _filter;
    private readonly IOptions<SafetyOptions> _options;
    private readonly ILogger<SafetyEvaluationService> _logger;

    /// <summary>
    /// Initializes a new instance of the SafetyEvaluationService class.
    /// </summary>
    /// <param name="evaluator">The primary safety evaluator.</param>
    /// <param name="options">The safety configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="filter">Optional safety filter for content sanitization.</param>
    public SafetyEvaluationService(
        ISafetyEvaluator evaluator,
        IOptions<SafetyOptions> options,
        ILogger<SafetyEvaluationService> logger,
        ISafetyFilter? filter = null)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filter = filter;
    }

    /// <summary>
    /// Evaluates user input for safety violations.
    /// </summary>
    /// <param name="message">The user message to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A safety evaluation result.</returns>
    public async Task<SafetyEvaluationResult> EvaluateUserInputAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Value.Enabled)
        {
            return SafetyEvaluationResult.Safe;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return SafetyEvaluationResult.Safe;
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Evaluating user input for safety. Message length: {Length}", message.Length);

            var result = await _evaluator.EvaluateTextAsync(message, cancellationToken);
            
            stopwatch.Stop();
            
            if (!result.IsSafe)
            {
                _logger.LogWarning("User input blocked due to {Categories} with risk score {RiskScore}. Processing time: {ElapsedMs}ms",
                    result.DetectedCategories.Select(c => c.Category), result.RiskScore, stopwatch.ElapsedMilliseconds);
                
                await LogSafetyViolationAsync(message, result, "UserInput");
            }
            else
            {
                _logger.LogDebug("User input evaluated as safe. Processing time: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error occurred during user input safety evaluation after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return GetFallbackResult("UserInput", ex);
        }
    }

    /// <summary>
    /// Evaluates AI-generated output for safety violations.
    /// </summary>
    /// <param name="output">The AI-generated content to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A safety evaluation result.</returns>
    public async Task<SafetyEvaluationResult> EvaluateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        if (!_options.Value.Enabled)
        {
            return SafetyEvaluationResult.Safe;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return SafetyEvaluationResult.Safe;
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Evaluating AI output for safety. Output length: {Length}", output.Length);

            var result = await _evaluator.EvaluateTextAsync(output, cancellationToken);
            
            stopwatch.Stop();
            
            if (!result.IsSafe)
            {
                _logger.LogWarning("AI output blocked due to {Categories} with risk score {RiskScore}. Processing time: {ElapsedMs}ms",
                    result.DetectedCategories.Select(c => c.Category), result.RiskScore, stopwatch.ElapsedMilliseconds);
                
                await LogSafetyViolationAsync(output, result, "AIOutput");
            }
            else
            {
                _logger.LogDebug("AI output evaluated as safe. Processing time: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error occurred during AI output safety evaluation after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return GetFallbackResult("AIOutput", ex);
        }
    }

    /// <summary>
    /// Evaluates multiple messages in batch for improved performance.
    /// </summary>
    /// <param name="messages">The collection of messages to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A collection of safety evaluation results.</returns>
    public async Task<IReadOnlyList<SafetyEvaluationResult>> EvaluateBatchAsync(
        IEnumerable<string> messages,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Value.Enabled)
        {
            return messages.Select(_ => SafetyEvaluationResult.Safe).ToList();
        }

        var messageList = messages.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
        
        if (!messageList.Any())
        {
            return new List<SafetyEvaluationResult>();
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Starting batch safety evaluation of {Count} messages", messageList.Count);

            var results = await _evaluator.EvaluateBatchAsync(messageList, cancellationToken);
            
            stopwatch.Stop();
            
            var violationCount = results.Count(r => !r.IsSafe);
            
            if (violationCount > 0)
            {
                _logger.LogWarning("Batch evaluation completed with {ViolationCount} violations out of {TotalCount} messages. Processing time: {ElapsedMs}ms",
                    violationCount, messageList.Count, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug("Batch evaluation completed with no violations. Processing time: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }

            return results;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error occurred during batch safety evaluation after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            // Return fallback results for all messages
            return messageList.Select(_ => GetFallbackResult("Batch", ex)).ToList();
        }
    }

    /// <summary>
    /// Creates a new streaming evaluator for real-time content analysis.
    /// </summary>
    /// <returns>A streaming safety evaluator instance.</returns>
    public IStreamingSafetyEvaluator CreateStreamingEvaluator()
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogDebug("Safety is disabled, creating no-op streaming evaluator");
            return new NoOpStreamingSafetyEvaluator();
        }

        _logger.LogDebug("Creating a new streaming safety evaluator using provider: {Provider}", _evaluator.GetProviderName());
        return _evaluator.CreateStreamingEvaluator();
    }

    /// <summary>
    /// Filters and sanitizes text content if a filter is available.
    /// </summary>
    /// <param name="text">The text to filter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A filtered text result.</returns>
    public async Task<FilteredTextResult?> FilterTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_filter == null)
        {
            _logger.LogDebug("No safety filter configured, skipping content filtering");
            return null;
        }

        if (!_options.Value.Enabled)
        {
            return new FilteredTextResult
            {
                OriginalText = text,
                FilteredText = text,
                WasFiltered = false
            };
        }

        try
        {
            _logger.LogDebug("Filtering text content. Length: {Length}", text.Length);
            
            var result = await _filter.FilterTextAsync(text, cancellationToken);
            
            if (result.WasFiltered)
            {
                _logger.LogInformation("Text content was filtered. {ActionCount} actions applied", result.AppliedActions.Count);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during text filtering");
            return null;
        }
    }

    /// <summary>
    /// Gets the safety configuration and status information.
    /// </summary>
    /// <returns>Safety status information.</returns>
    public SafetyStatus GetSafetyStatus()
    {
        return new SafetyStatus
        {
            IsEnabled = _options.Value.Enabled,
            Provider = _evaluator.GetProviderName(),
            SupportedCategories = _evaluator.GetSupportedCategories(),
            HasFilter = _filter != null,
            FilterProvider = _filter?.GetProviderName(),
            FallbackBehavior = _options.Value.FallbackBehavior,
            InputPolicy = _options.Value.InputPolicy,
            OutputPolicy = _options.Value.OutputPolicy
        };
    }

    /// <summary>
    /// Logs safety violations for audit and compliance purposes.
    /// </summary>
    /// <param name="content">The content that triggered the violation.</param>
    /// <param name="result">The safety evaluation result.</param>
    /// <param name="contentType">The type of content (e.g., "UserInput", "AIOutput").</param>
    private Task LogSafetyViolationAsync(string content, SafetyEvaluationResult result, string contentType)
    {
        try
        {
            // Create audit log entry
            var auditEntry = new
            {
                Timestamp = DateTimeOffset.UtcNow,
                ContentType = contentType,
                ContentLength = content.Length,
                ContentHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)),
                IsSafe = result.IsSafe,
                RiskScore = result.RiskScore,
                DetectedCategories = result.DetectedCategories.Select(c => new
                {
                    Category = c.Category.ToString(),
                    Severity = c.Severity,
                    Confidence = c.Confidence,
                    Description = c.Description
                }),
                Recommendations = result.Recommendations,
                Provider = result.Metadata?.Provider,
                ProcessingTimeMs = result.Metadata?.ProcessingTimeMs,
                RequestId = result.Metadata?.RequestId
            };

            // Log the audit entry (in a real implementation, this would go to a secure audit system)
            _logger.LogInformation("Safety violation detected: {@AuditEntry}", auditEntry);

            // TODO: Implement additional audit logging to secure storage
            // TODO: Implement alerting for high-severity violations
            // TODO: Implement rate limiting for repeated violations
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log safety violation for audit");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a fallback result when the service fails.
    /// </summary>
    /// <param name="context">The context in which the failure occurred.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A fallback safety evaluation result.</returns>
    private SafetyEvaluationResult GetFallbackResult(string context, Exception? exception = null)
    {
        if (_options.Value.FallbackBehavior == FallbackBehavior.FailClosed)
        {
            return new SafetyEvaluationResult
            {
                IsSafe = false,
                RiskScore = 80,
                DetectedCategories = { new DetectedHarmCategory { Category = HarmCategory.Violence, Severity = 6 } },
                Recommendations = { $"Content blocked due to safety service failure in {context}." },
                Metadata = new EvaluationMetadata
                {
                    Provider = _evaluator.GetProviderName(),
                    AdditionalData = {
                        ["FallbackReason"] = exception?.Message ?? "Service unavailable",
                        ["Context"] = context,
                        ["FallbackBehavior"] = "FailClosed"
                    }
                }
            };
        }

        return new SafetyEvaluationResult
        {
            IsSafe = true,
            RiskScore = 0,
            Recommendations = { $"Content allowed due to safety service failure in {context} (fail-open policy)." },
            Metadata = new EvaluationMetadata
            {
                Provider = _evaluator.GetProviderName(),
                AdditionalData = {
                    ["FallbackReason"] = exception?.Message ?? "Service unavailable",
                    ["Context"] = context,
                    ["FallbackBehavior"] = "FailOpen"
                }
            }
        };
    }
}

/// <summary>
/// Provides status information about the safety service configuration.
/// </summary>
public class SafetyStatus
{
    public bool IsEnabled { get; set; }
    public string Provider { get; set; } = string.Empty;
    public ISet<HarmCategory> SupportedCategories { get; set; } = new HashSet<HarmCategory>();
    public bool HasFilter { get; set; }
    public string? FilterProvider { get; set; }
    public FallbackBehavior FallbackBehavior { get; set; }
    public PolicySettings InputPolicy { get; set; } = new();
    public PolicySettings OutputPolicy { get; set; } = new();
}

/// <summary>
/// A no-op implementation used when safety is disabled.
/// </summary>
internal class NoOpStreamingSafetyEvaluator : IStreamingSafetyEvaluator
{
    private bool _disposed = false;

    public Task<SafetyEvaluationResult> EvaluateChunkAsync(string chunk, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SafetyEvaluationResult.Safe);
    }

    public string GetAccumulatedContent() => string.Empty;

    public void Reset() { /* No-op */ }

    public int GetProcessedChunkCount() => 0;

    public bool HasViolations() => false;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}