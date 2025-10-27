namespace AIChat.Safety.Contracts;

/// <summary>
/// Defines the contract for streaming safety evaluators that can analyze text chunks in real-time.
/// </summary>
public interface IStreamingSafetyEvaluator : IDisposable
{
    /// <summary>
    /// Evaluates the next chunk of text in the stream, considering previous chunks.
    /// </summary>
    /// <param name="chunk">The next text chunk to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A safety evaluation result for the current accumulated content.</returns>
    Task<SafetyEvaluationResult> EvaluateChunkAsync(string chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current accumulated text content being evaluated.
    /// </summary>
    /// <returns>The accumulated text content.</returns>
    string GetAccumulatedContent();

    /// <summary>
    /// Resets the evaluator state, clearing all accumulated content.
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets the total number of chunks processed so far.
    /// </summary>
    /// <returns>The number of processed chunks.</returns>
    int GetProcessedChunkCount();

    /// <summary>
    /// Gets whether the evaluator has detected any violations in the processed content.
    /// </summary>
    /// <returns>True if violations have been detected, false otherwise.</returns>
    bool HasViolations();
}