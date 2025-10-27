using AIChat.Safety.Contracts;
using AIChat.Safety.Options;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;

namespace AIChat.Safety.Providers;

/// <summary>
/// OpenAI Moderation API implementation of the ISafetyEvaluator interface.
/// Provides comprehensive text safety evaluation using OpenAI's Moderation API.
/// </summary>
public class OpenAIModerationEvaluator : ISafetyEvaluator
{
    private readonly HttpClient _httpClient;
    private readonly SafetyOptions _options;
    private readonly ILogger<OpenAIModerationEvaluator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _providerName = "OpenAI Moderation";

    /// <summary>
    /// Initializes a new instance of the OpenAIModerationEvaluator class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for making API requests.</param>
    /// <param name="options">The safety configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="loggerFactory">The logger factory for creating streaming evaluators.</param>
    public OpenAIModerationEvaluator(
        HttpClient httpClient,
        IOptions<SafetyOptions> options,
        ILogger<OpenAIModerationEvaluator> logger,
        ILoggerFactory loggerFactory)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        ConfigureHttpClient();
    }

    /// <summary>
    /// Evaluates a single text block for safety violations.
    /// </summary>
    /// <param name="text">The text to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A comprehensive safety evaluation result.</returns>
    public async Task<SafetyEvaluationResult> EvaluateTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return SafetyEvaluationResult.Safe;
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Starting text evaluation with OpenAI Moderation. Text length: {Length}", text.Length);

            var request = CreateModerationRequest(text);
            var response = await SendModerationRequestAsync(request, cancellationToken);
            stopwatch.Stop();

            var result = MapResponseToResult(response, _options.InputPolicy.Thresholds);
            result.Metadata = new EvaluationMetadata
            {
                Provider = _providerName,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RequestId = response.Id,
                AdditionalData = { ["Model"] = response.Model }
            };

            _logger.LogDebug("Text evaluation completed in {ElapsedMs}ms. Result: {IsSafe}",
                stopwatch.ElapsedMilliseconds, result.IsSafe);

            return result;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "OpenAI Moderation request failed after {ElapsedMs}ms. Status: {Status}",
                stopwatch.ElapsedMilliseconds, ex.StatusCode);
            return GetFallbackResult(ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error occurred during text evaluation after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);
            return GetFallbackResult(ex);
        }
    }

    /// <summary>
    /// Evaluates multiple text items in batch for improved performance.
    /// </summary>
    /// <param name="texts">The collection of texts to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A collection of safety evaluation results.</returns>
    public async Task<IReadOnlyList<SafetyEvaluationResult>> EvaluateBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return texts.Select(_ => SafetyEvaluationResult.Safe).ToList();
        }

        var textList = texts.ToList();
        var results = new List<SafetyEvaluationResult>(textList.Count);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Starting batch evaluation of {Count} texts", textList.Count);

        // Process texts in parallel for better performance
        var tasks = textList.Select(text => EvaluateTextAsync(text, cancellationToken));
        results.AddRange(await Task.WhenAll(tasks));

        stopwatch.Stop();
        _logger.LogDebug("Batch evaluation of {Count} texts completed in {ElapsedMs}ms",
            textList.Count, stopwatch.ElapsedMilliseconds);

        return results.AsReadOnly();
    }

    /// <summary>
    /// Creates a new streaming evaluator for real-time content analysis.
    /// </summary>
    /// <returns>A streaming safety evaluator instance.</returns>
    public IStreamingSafetyEvaluator CreateStreamingEvaluator()
    {
        return new OpenAIStreamingSafetyEvaluator(_httpClient, _options, _loggerFactory.CreateLogger<OpenAIStreamingSafetyEvaluator>());
    }

    /// <summary>
    /// Gets the set of harm categories supported by this evaluator.
    /// </summary>
    /// <returns>A set of supported harm categories.</returns>
    public ISet<HarmCategory> GetSupportedCategories()
    {
        return new HashSet<HarmCategory>
        {
            HarmCategory.Hate,
            HarmCategory.SelfHarm,
            HarmCategory.Sexual,
            HarmCategory.Violence
        };
    }

    /// <summary>
    /// Gets the name of the safety provider.
    /// </summary>
    /// <returns>The provider name.</returns>
    public string GetProviderName()
    {
        return _providerName;
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

        // Add general recommendations if unsafe
        if (!evaluationResult.IsSafe && !evaluationResult.Recommendations.Any())
        {
            evaluationResult.Recommendations.Add("Content contains potentially harmful material and should be reviewed.");
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

        return $"{category} content detected with {severityText} severity (level {severity}).";
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
            HarmCategory.Hate => "Content contains hate speech and should be blocked or heavily moderated.",
            HarmCategory.SelfHarm => "Content contains self-harm references and requires immediate attention.",
            HarmCategory.Sexual => "Content contains sexually explicit material and should be restricted.",
            HarmCategory.Violence => "Content contains violent material and should be age-restricted or blocked.",
            _ => "Content contains potentially harmful material and should be reviewed."
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
        var categoryMultiplier = Math.Min(detectedCategories.Count, 3); // Cap at 3x multiplier
        
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
                Recommendations = { "Content blocked due to safety service unavailability." },
                Metadata = new EvaluationMetadata
                {
                    Provider = _providerName,
                    AdditionalData = { ["FallbackReason"] = exception?.Message ?? "Service unavailable" }
                }
            };
        }

        return new SafetyEvaluationResult
        {
            IsSafe = true,
            RiskScore = 0,
            Recommendations = { "Content allowed due to safety service unavailability (fail-open policy)." },
            Metadata = new EvaluationMetadata
            {
                Provider = _providerName,
                AdditionalData = { ["FallbackReason"] = exception?.Message ?? "Service unavailable" }
            }
        };
    }
}

/// <summary>
/// Represents the OpenAI Moderation API response.
/// </summary>
internal class OpenAIModerationResponse
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<OpenAIModerationResult>? Results { get; set; }
}

/// <summary>
/// Represents a single moderation result from OpenAI.
/// </summary>
internal class OpenAIModerationResult
{
    public bool Flagged { get; set; }
    public List<string>? Categories { get; set; }
    public bool Hate { get; set; }
    public double Hate_score { get; set; }
    public bool Self_harm { get; set; }
    public double Self_harm_score { get; set; }
    public bool Sexual { get; set; }
    public double Sexual_score { get; set; }
    public bool Violence { get; set; }
    public double Violence_score { get; set; }
}