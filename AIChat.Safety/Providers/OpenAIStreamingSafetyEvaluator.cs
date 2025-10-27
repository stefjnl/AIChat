using AIChat.Safety.Contracts;
using AIChat.Safety.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace AIChat.Safety.Providers;

/// <summary>
/// OpenAI Moderation API implementation of the IStreamingSafetyEvaluator interface.
/// Provides real-time streaming text safety evaluation using OpenAI's Moderation API.
/// </summary>
internal class OpenAIStreamingSafetyEvaluator : IStreamingSafetyEvaluator
{
    private readonly HttpClient _httpClient;
    private readonly SafetyOptions _options;
    private readonly ILogger<OpenAIStreamingSafetyEvaluator> _logger;
    private readonly StringBuilder _buffer;
    private readonly string _providerName = "OpenAI Moderation Streaming";
    private readonly object _lock = new object();
    
    private int _chunkCount = 0;
    private bool _hasViolations = false;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the OpenAIStreamingSafetyEvaluator class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for making API requests.</param>
    /// <param name="options">The safety configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenAIStreamingSafetyEvaluator(
        HttpClient httpClient,
        SafetyOptions options,
        ILogger<OpenAIStreamingSafetyEvaluator> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _buffer = new StringBuilder();

        ConfigureHttpClient();
    }

    /// <summary>
    /// Evaluates the next chunk of text in the stream, considering previous chunks.
    /// </summary>
    /// <param name="chunk">The next text chunk to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A safety evaluation result for the current accumulated content.</returns>
    public async Task<SafetyEvaluationResult> EvaluateChunkAsync(string chunk, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OpenAIStreamingSafetyEvaluator));
        }

        if (!_options.Enabled)
        {
            return SafetyEvaluationResult.Safe;
        }

        if (string.IsNullOrWhiteSpace(chunk))
        {
            return SafetyEvaluationResult.Safe;
        }

        lock (_lock)
        {
            _chunkCount++;
            _buffer.Append(chunk);
        }

        _logger.LogDebug("Processing chunk #{ChunkCount} of length {Length}", _chunkCount, chunk.Length);

        // Strategy: Evaluate after certain conditions are met
        if (ShouldEvaluateBuffer())
        {
            var textToEvaluate = GetAccumulatedContent();
            if (!string.IsNullOrWhiteSpace(textToEvaluate))
            {
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    _logger.LogDebug("Starting streaming evaluation of {Length} characters", textToEvaluate.Length);

                    var request = CreateModerationRequest(textToEvaluate);
                    var response = await SendModerationRequestAsync(request, cancellationToken);
                    stopwatch.Stop();

                    var result = MapResponseToResult(response, _options.OutputPolicy.Thresholds);
                    result.Metadata = new EvaluationMetadata
                    {
                        Provider = _providerName,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        RequestId = response.Id,
                        AdditionalData = new Dictionary<string, object>
                        {
                            ["ChunkNumber"] = _chunkCount,
                            ["EvaluationType"] = "Streaming",
                            ["BufferLength"] = textToEvaluate.Length,
                            ["Model"] = response.Model
                        }
                    };

                    if (!result.IsSafe)
                    {
                        lock (_lock)
                        {
                            _hasViolations = true;
                        }
                        
                        _logger.LogWarning("Streaming evaluation detected violations in chunk #{ChunkCount}: {Categories}",
                            _chunkCount, result.DetectedCategories.Select(c => c.Category));
                        
                        return result;
                    }

                    // If safe and we've evaluated a complete unit, clear the evaluated portion
                    ClearEvaluatedContent();

                    _logger.LogDebug("Streaming evaluation completed in {ElapsedMs}ms. Result: {IsSafe}",
                        stopwatch.ElapsedMilliseconds, result.IsSafe);

                    return result;
                }
                catch (HttpRequestException ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "OpenAI Moderation streaming request failed after {ElapsedMs}ms. Status: {Status}",
                        stopwatch.ElapsedMilliseconds, ex.StatusCode);
                    return GetFallbackResult(ex);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Unexpected error occurred during streaming evaluation after {ElapsedMs}ms",
                        stopwatch.ElapsedMilliseconds);
                    return GetFallbackResult(ex);
                }
            }
        }

        return SafetyEvaluationResult.Safe;
    }

    /// <summary>
    /// Gets the current accumulated text content being evaluated.
    /// </summary>
    /// <returns>The accumulated text content.</returns>
    public string GetAccumulatedContent()
    {
        lock (_lock)
        {
            return _buffer.ToString();
        }
    }

    /// <summary>
    /// Resets the evaluator state, clearing all accumulated content.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _buffer.Clear();
            _chunkCount = 0;
            _hasViolations = false;
        }
        
        _logger.LogDebug("Streaming evaluator state reset");
    }

    /// <summary>
    /// Gets the total number of chunks processed so far.
    /// </summary>
    /// <returns>The number of processed chunks.</returns>
    public int GetProcessedChunkCount()
    {
        lock (_lock)
        {
            return _chunkCount;
        }
    }

    /// <summary>
    /// Gets whether the evaluator has detected any violations in the processed content.
    /// </summary>
    /// <returns>True if violations have been detected, false otherwise.</returns>
    public bool HasViolations()
    {
        lock (_lock)
        {
            return _hasViolations;
        }
    }

    /// <summary>
    /// Configures the HTTP client for OpenAI API requests.
    /// </summary>
    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.Endpoint);
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.Resilience.TimeoutInMilliseconds);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        
        if (!string.IsNullOrEmpty(_options.OrganizationId))
        {
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", _options.OrganizationId);
        }
    }

    /// <summary>
    /// Determines if the buffer should be evaluated based on content and configuration.
    /// </summary>
    /// <returns>True if evaluation should proceed, false otherwise.</returns>
    private bool ShouldEvaluateBuffer()
    {
        var text = GetAccumulatedContent();
        
        // Multiple strategies for triggering evaluation:
        // 1. Character count threshold
        if (text.Length > 300)
        {
            return true;
        }

        // 2. Sentence boundaries (using regex for better detection)
        if (Regex.IsMatch(text, @"[.!?]+\s*$"))
        {
            return true;
        }

        // 3. Line breaks or paragraph boundaries
        if (text.Contains("\n\n") || text.Contains(Environment.NewLine + Environment.NewLine))
        {
            return true;
        }

        // 4. Time-based evaluation (evaluate every 10 chunks regardless of content)
        return _chunkCount % 10 == 0;
    }

    /// <summary>
    /// Clears the evaluated content from the buffer, keeping some context for continuity.
    /// </summary>
    private void ClearEvaluatedContent()
    {
        lock (_lock)
        {
            var currentContent = _buffer.ToString();
            
            // Keep the last 50 characters for context to ensure continuity across evaluations
            if (currentContent.Length > 50)
            {
                _buffer.Clear();
                _buffer.Append(currentContent[^50..]);
            }
        }
    }

    /// <summary>
    /// Creates a moderation request object.
    /// </summary>
    /// <param name="text">The text to moderate.</param>
    /// <returns>A moderation request object.</returns>
    private object CreateModerationRequest(string text)
    {
        return new
        {
            input = text,
            model = _options.Model
        };
    }

    /// <summary>
    /// Sends a moderation request to the OpenAI API.
    /// </summary>
    /// <param name="request">The moderation request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The moderation response.</returns>
    private async Task<OpenAIModerationResponse> SendModerationRequestAsync(object request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var moderationResponse = JsonSerializer.Deserialize<OpenAIModerationResponse>(responseJson, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return moderationResponse ?? throw new InvalidOperationException("Failed to deserialize moderation response");
    }

    /// <summary>
    /// Maps OpenAI Moderation response to our safety evaluation result format.
    /// </summary>
    /// <param name="response">The OpenAI moderation response.</param>
    /// <param name="thresholds">The configured thresholds.</param>
    /// <returns>A comprehensive safety evaluation result.</returns>
    private SafetyEvaluationResult MapResponseToResult(OpenAIModerationResponse response, Dictionary<HarmCategory, int> thresholds)
    {
        var evaluationResult = new SafetyEvaluationResult { IsSafe = true };
        var maxScore = 0.0;

        if (response.Results?.Any() == true)
        {
            var result = response.Results.First();
            
            // Check each category
            CheckCategory(result, "hate", HarmCategory.Hate, thresholds, evaluationResult, ref maxScore);
            CheckCategory(result, "self_harm", HarmCategory.SelfHarm, thresholds, evaluationResult, ref maxScore);
            CheckCategory(result, "sexual", HarmCategory.Sexual, thresholds, evaluationResult, ref maxScore);
            CheckCategory(result, "violence", HarmCategory.Violence, thresholds, evaluationResult, ref maxScore);
        }

        // Calculate overall risk score based on category scores
        evaluationResult.RiskScore = CalculateRiskScore(evaluationResult.DetectedCategories, maxScore);

        // Add streaming-specific recommendations
        if (!evaluationResult.IsSafe)
        {
            evaluationResult.Recommendations.Insert(0, "Streaming content violation detected. Consider terminating the stream.");
        }

        return evaluationResult;
    }

    /// <summary>
    /// Checks a specific category and adds violations if thresholds are met.
    /// </summary>
    /// <param name="result">The OpenAI moderation result.</param>
    /// <param name="categoryName">The OpenAI category name.</param>
    /// <param name="harmCategory">Our harm category enum.</param>
    /// <param name="thresholds">The configured thresholds.</param>
    /// <param name="evaluationResult">The evaluation result to update.</param>
    /// <param name="maxScore">The maximum score seen so far.</param>
    private void CheckCategory(
        OpenAIModerationResult result, 
        string categoryName, 
        HarmCategory harmCategory,
        Dictionary<HarmCategory, int> thresholds,
        SafetyEvaluationResult evaluationResult,
        ref double maxScore)
    {
        var flagged = GetCategoryFlagged(result, categoryName);
        var score = GetCategoryScore(result, categoryName);

        if (flagged)
        {
            maxScore = Math.Max(maxScore, score);
            var severity = CalculateSeverity(score);

            var detectedHarm = new DetectedHarmCategory
            {
                Category = harmCategory,
                Severity = severity,
                Confidence = CalculateConfidence(score),
                Description = GetCategoryDescription(harmCategory, severity)
            };

            // Check if the severity meets or exceeds the threshold
            if (thresholds.TryGetValue(harmCategory, out var threshold) && severity >= threshold)
            {
                evaluationResult.IsSafe = false;
                evaluationResult.DetectedCategories.Add(detectedHarm);
                evaluationResult.Recommendations.Add(GetRecommendation(harmCategory, severity));
            }
        }
    }

    /// <summary>
    /// Gets the flagged status for a category from the OpenAI result.
    /// </summary>
    /// <param name="result">The OpenAI moderation result.</param>
    /// <param name="categoryName">The category name.</param>
    /// <returns>True if the category is flagged.</returns>
    private static bool GetCategoryFlagged(OpenAIModerationResult result, string categoryName)
    {
        var property = result.GetType().GetProperty($"{categoryName}");
        return property?.GetValue(result) as bool? ?? false;
    }

    /// <summary>
    /// Gets the category score from the OpenAI result.
    /// </summary>
    /// <param name="result">The OpenAI moderation result.</param>
    /// <param name="categoryName">The category name.</param>
    /// <returns>The category score.</returns>
    private static double GetCategoryScore(OpenAIModerationResult result, string categoryName)
    {
        var property = result.GetType().GetProperty($"{categoryName}_score");
        return property?.GetValue(result) as double? ?? 0.0;
    }

    /// <summary>
    /// Calculates severity level based on OpenAI score (0-1).
    /// </summary>
    /// <param name="score">The OpenAI score.</param>
    /// <returns>A severity level (0-7).</returns>
    private static int CalculateSeverity(double score)
    {
        return score switch
        {
            <= 0.1 => 0,
            <= 0.2 => 1,
            <= 0.3 => 2,
            <= 0.4 => 3,
            <= 0.5 => 4,
            <= 0.6 => 5,
            <= 0.8 => 6,
            <= 1.0 => 7,
            _ => 0
        };
    }

    /// <summary>
    /// Calculates confidence score based on OpenAI score.
    /// </summary>
    /// <param name="score">The OpenAI score (0-1).</param>
    /// <returns>A confidence score (0-100).</returns>
    private static int CalculateConfidence(double score)
    {
        return (int)(score * 100);
    }

    /// <summary>
    /// Gets a description for a detected harm category.
    /// </summary>
    /// <param name="category">The harm category.</param>
    /// <param name="severity">The severity level.</param>
    /// <returns>A description of the detection.</returns>
    private static string GetCategoryDescription(HarmCategory category, int severity)
    {
        var severityText = severity switch
        {
            <= 2 => "low",
            <= 4 => "medium",
            <= 6 => "high",
            _ => "very high"
        };

        return $"{category} content detected with {severityText} severity (level {severity}) in streaming content.";
    }

    /// <summary>
    /// Gets a recommendation for handling detected harmful content.
    /// </summary>
    /// <param name="category">The harm category.</param>
    /// <param name="severity">The severity level.</param>
    /// <returns>A recommendation string.</returns>
    private static string GetRecommendation(HarmCategory category, int severity)
    {
        return category switch
        {
            HarmCategory.Hate => "Streaming content contains hate speech and should be terminated immediately.",
            HarmCategory.SelfHarm => "Streaming content contains self-harm references and requires immediate intervention.",
            HarmCategory.Sexual => "Streaming content contains sexually explicit material and should be blocked.",
            HarmCategory.Violence => "Streaming content contains violent material and should be terminated.",
            _ => "Streaming content contains potentially harmful material and should be reviewed."
        };
    }

    /// <summary>
    /// Calculates an overall risk score based on detected categories and scores.
    /// </summary>
    /// <param name="detectedCategories">The detected harm categories.</param>
    /// <param name="maxScore">The maximum score detected.</param>
    /// <returns>A risk score (0-100).</returns>
    private static int CalculateRiskScore(List<DetectedHarmCategory> detectedCategories, double maxScore)
    {
        if (!detectedCategories.Any())
        {
            return 0;
        }

        // Base score on maximum score, adjusted by number of categories
        var baseScore = (int)(maxScore * 100);
        var categoryMultiplier = Math.Min(detectedCategories.Count, 3);
        
        return Math.Min(100, baseScore * categoryMultiplier);
    }

    /// <summary>
    /// Gets a fallback result when the service fails.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A fallback safety evaluation result.</returns>
    private SafetyEvaluationResult GetFallbackResult(Exception? exception = null)
    {
        if (_options.FallbackBehavior == FallbackBehavior.FailClosed)
        {
            return new SafetyEvaluationResult
            {
                IsSafe = false,
                RiskScore = 70,
                DetectedCategories = { new DetectedHarmCategory { Category = HarmCategory.Violence, Severity = 6 } },
                Recommendations = { "Streaming content blocked due to safety service unavailability." },
                Metadata = new EvaluationMetadata
                {
                    Provider = _providerName,
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["FallbackReason"] = exception?.Message ?? "Service unavailable",
                        ["EvaluationType"] = "Streaming"
                    }
                }
            };
        }

        return new SafetyEvaluationResult
        {
            IsSafe = true,
            RiskScore = 0,
            Recommendations = { "Streaming content allowed due to safety service unavailability (fail-open policy)." },
            Metadata = new EvaluationMetadata
            {
                Provider = _providerName,
                AdditionalData = new Dictionary<string, object>
                {
                    ["FallbackReason"] = exception?.Message ?? "Service unavailable",
                    ["EvaluationType"] = "Streaming"
                }
            }
        };
    }

    /// <summary>
    /// Dispose of the streaming evaluator resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogDebug("Disposing streaming evaluator after processing {ChunkCount} chunks", _chunkCount);
            Reset();
            _disposed = true;
        }
    }
}