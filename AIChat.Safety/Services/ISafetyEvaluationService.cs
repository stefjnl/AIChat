using AIChat.Safety.Contracts;

namespace AIChat.Safety.Services;

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
