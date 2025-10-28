using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AIChat.Safety.Services;
using AIChat.Safety.Contracts;
using AIChat.Safety.Telemetry;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace AIChat.Safety.Middleware;

/// <summary>
/// Chat client middleware that performs safety evaluation on messages using DelegatingChatClient pattern.
/// This middleware intercepts both user input and AI responses to ensure content safety.
/// </summary>
public class SafetyChatClientMiddleware : DelegatingChatClient
{
    private readonly ISafetyEvaluationService _safetyService;
    private readonly ILogger<SafetyChatClientMiddleware> _logger;
    private static readonly ActivitySource ActivitySource = new("AIChat.Safety.Middleware", "1.0.0");

    /// <summary>
    /// Initializes a new instance of the SafetyChatClientMiddleware class.
    /// </summary>
    /// <param name="innerClient">The inner chat client to delegate to after safety checks.</param>
    /// <param name="safetyService">The safety evaluation service.</param>
    /// <param name="logger">The logger instance.</param>
    public SafetyChatClientMiddleware(
        IChatClient innerClient,
        ISafetyEvaluationService safetyService,
        ILogger<SafetyChatClientMiddleware> logger)
        : base(innerClient)
    {
        _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Completes a chat interaction with safety evaluation on both input and output.
    /// </summary>
 /// <param name="chatMessages">The chat messages representing the conversation history.</param>
    /// <param name="options">Optional chat completion options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chat completion result if safe, otherwise throws an exception.</returns>
    /// <exception cref="SafetyViolationException">Thrown when content violates safety policies.</exception>
    public override async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
     ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
  using var activity = ActivitySource.StartActivity("SafetyMiddleware.CompleteAsync", ActivityKind.Server);
  activity?.SetTag("safety.message_count", chatMessages.Count);

        _logger.LogDebug("SafetyChatClientMiddleware: Evaluating user input for {MessageCount} messages", chatMessages.Count);

        // Evaluate the last user message (most recent input)
   var lastUserMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
        if (lastUserMessage != null && !string.IsNullOrEmpty(lastUserMessage.Text))
        {
     var userInputEvaluation = await _safetyService.EvaluateUserInputAsync(
                lastUserMessage.Text,
  cancellationToken);

  if (!userInputEvaluation.IsSafe)
      {
     _logger.LogWarning(
     "User input blocked by safety middleware. Categories: {Categories}, RiskScore: {RiskScore}",
       userInputEvaluation.DetectedCategories.Select(c => c.Category),
       userInputEvaluation.RiskScore);

     activity?.SetTag("safety.user_input_blocked", true);
        activity?.SetTag("safety.risk_score", userInputEvaluation.RiskScore);
          activity?.SetTag("safety.violated_categories", 
string.Join(",", userInputEvaluation.DetectedCategories.Select(c => c.Category)));

          throw new SafetyViolationException(
    "User message blocked due to safety violations",
    userInputEvaluation,
     SafetyViolationType.UserInput);
    }

   activity?.SetTag("safety.user_input_safe", true);
   _logger.LogDebug("User input passed safety evaluation");
     }

     // Call the inner client to get AI response
        var completion = await base.CompleteAsync(chatMessages, options, cancellationToken);

        // Evaluate AI response
  if (!string.IsNullOrEmpty(completion.Message.Text))
        {
     var outputEvaluation = await _safetyService.EvaluateOutputAsync(
   completion.Message.Text,
   cancellationToken);

if (!outputEvaluation.IsSafe)
      {
          _logger.LogWarning(
        "AI output blocked by safety middleware. Categories: {Categories}, RiskScore: {RiskScore}",
       outputEvaluation.DetectedCategories.Select(c => c.Category),
     outputEvaluation.RiskScore);

    activity?.SetTag("safety.ai_output_blocked", true);
             activity?.SetTag("safety.risk_score", outputEvaluation.RiskScore);
  activity?.SetTag("safety.violated_categories",
string.Join(",", outputEvaluation.DetectedCategories.Select(c => c.Category)));

      throw new SafetyViolationException(
       "AI response blocked due to safety violations",
     outputEvaluation,
SafetyViolationType.AIOutput);
    }

  activity?.SetTag("safety.ai_output_safe", true);
       _logger.LogDebug("AI output passed safety evaluation");
        }

        return completion;
    }

    /// <summary>
    /// Completes a streaming chat interaction with real-time safety evaluation.
    /// </summary>
    /// <param name="chatMessages">The chat messages representing the conversation history.</param>
    /// <param name="options">Optional chat completion options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of streaming chat completion updates.</returns>
    /// <exception cref="SafetyViolationException">Thrown when content violates safety policies.</exception>
 public override async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SafetyChatClientMiddleware: Evaluating streaming input for {MessageCount} messages", chatMessages.Count);

      // Evaluate the last user message before streaming
        var lastUserMessage = chatMessages.LastOrDefault(m => m.Role == ChatRole.User);
if (lastUserMessage != null && !string.IsNullOrEmpty(lastUserMessage.Text))
        {
var userInputEvaluation = await _safetyService.EvaluateUserInputAsync(
        lastUserMessage.Text,
cancellationToken);

            if (!userInputEvaluation.IsSafe)
            {
_logger.LogWarning(
      "User input blocked by safety middleware (streaming). Categories: {Categories}, RiskScore: {RiskScore}",
   userInputEvaluation.DetectedCategories.Select(c => c.Category),
        userInputEvaluation.RiskScore);

    throw new SafetyViolationException(
"User message blocked due to safety violations",
        userInputEvaluation,
      SafetyViolationType.UserInput);
            }

   _logger.LogDebug("User input passed safety evaluation (streaming)");
  }

  // Create streaming evaluator for real-time output evaluation
  using var streamingEvaluator = _safetyService.CreateStreamingEvaluator();
      var processedChunks = 0;

        _logger.LogDebug("Created streaming safety evaluator for AI response");

     // Stream responses with safety evaluation on each chunk
    await foreach (var update in base.CompleteStreamingAsync(chatMessages, options, cancellationToken))
        {
         // Evaluate each text chunk as it arrives
  if (!string.IsNullOrEmpty(update.Text))
      {
            processedChunks++;

         var chunkEvaluation = await streamingEvaluator.EvaluateChunkAsync(
           update.Text,
         cancellationToken);

             if (!chunkEvaluation.IsSafe)
   {
          _logger.LogWarning(
           "AI streaming output blocked after {ChunkCount} chunks. Categories: {Categories}, RiskScore: {RiskScore}",
   processedChunks,
               chunkEvaluation.DetectedCategories.Select(c => c.Category),
             chunkEvaluation.RiskScore);

      throw new SafetyViolationException(
           $"AI streaming response blocked after {processedChunks} chunks due to safety violations",
    chunkEvaluation,
     SafetyViolationType.AIOutput);
  }

          _logger.LogTrace("Streaming chunk {ChunkCount} passed safety evaluation", processedChunks);
     }

       yield return update;
        }

    _logger.LogDebug("Streaming completed successfully. Total chunks evaluated: {ChunkCount}", processedChunks);
  }

    /// <summary>
    /// Gets metadata about this middleware, including the inner client information.
    /// </summary>
    /// <param name="key">The metadata key to retrieve.</param>
    /// <returns>The metadata value if found, otherwise null.</returns>
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        // Allow retrieval of the safety service for inspection/testing
        if (serviceType == typeof(ISafetyEvaluationService))
        {
            return _safetyService;
        }

        return base.GetService(serviceType, serviceKey);
    }
}

/// <summary>
/// Exception thrown when content violates safety policies in the middleware.
/// </summary>
public class SafetyViolationException : InvalidOperationException
{
    /// <summary>
    /// Gets the safety evaluation result that triggered the violation.
    /// </summary>
    public SafetyEvaluationResult EvaluationResult { get; }

    /// <summary>
    /// Gets the type of violation (user input or AI output).
    /// </summary>
    public SafetyViolationType ViolationType { get; }

    /// <summary>
 /// Initializes a new instance of the SafetyViolationException class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="evaluationResult">The safety evaluation result.</param>
    /// <param name="violationType">The type of safety violation.</param>
 public SafetyViolationException(
        string message,
      SafetyEvaluationResult evaluationResult,
        SafetyViolationType violationType)
 : base(message)
    {
EvaluationResult = evaluationResult ?? throw new ArgumentNullException(nameof(evaluationResult));
        ViolationType = violationType;
    }

    /// <summary>
    /// Initializes a new instance of the SafetyViolationException class with an inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="evaluationResult">The safety evaluation result.</param>
    /// <param name="violationType">The type of safety violation.</param>
    /// <param name="innerException">The inner exception.</param>
    public SafetyViolationException(
        string message,
        SafetyEvaluationResult evaluationResult,
        SafetyViolationType violationType,
 Exception innerException)
        : base(message, innerException)
    {
        EvaluationResult = evaluationResult ?? throw new ArgumentNullException(nameof(evaluationResult));
        ViolationType = violationType;
    }
}

/// <summary>
/// Enum representing the type of safety violation.
/// </summary>
public enum SafetyViolationType
{
    /// <summary>
    /// Violation detected in user input.
    /// </summary>
    UserInput,

    /// <summary>
  /// Violation detected in AI output.
    /// </summary>
  AIOutput
}
