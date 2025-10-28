namespace AIChat.Safety.Contracts;

/// <summary>
/// Defines the contract for safety evaluators that can analyze text content for harmful material.
/// </summary>
public interface ISafetyEvaluator
{
    /// <summary>
    /// Evaluates a complete block of text for safety violations.
    /// </summary>
    /// <param name="text">The text content to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A safety evaluation result containing detected violations and metadata.</returns>
    Task<SafetyEvaluationResult> EvaluateTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates multiple text items in batch for improved performance.
    /// </summary>
    /// <param name="texts">The collection of text items to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A collection of safety evaluation results corresponding to the input texts.</returns>
    Task<IReadOnlyList<SafetyEvaluationResult>> EvaluateBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new stateful streaming evaluator for a single conversation stream.
    /// </summary>
    /// <returns>A streaming safety evaluator instance.</returns>
    IStreamingSafetyEvaluator CreateStreamingEvaluator();

    /// <summary>
    /// Gets the capabilities and supported categories of this evaluator.
    /// </summary>
    /// <returns>A set of harm categories that this evaluator can detect.</returns>
    ISet<HarmCategory> GetSupportedCategories();

    /// <summary>
    /// Gets the name of the safety provider.
    /// </summary>
    /// <returns>The provider name.</returns>
    string GetProviderName();
}