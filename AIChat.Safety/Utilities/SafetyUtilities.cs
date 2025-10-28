using AIChat.Safety.Contracts;

namespace AIChat.Safety.Utilities;

/// <summary>
/// Provides common utility methods for safety evaluation operations.
/// </summary>
public static class SafetyUtilities
{
    // Severity threshold constants
    public const double SeverityThreshold0 = 0.1;
    public const double SeverityThreshold1 = 0.2;
    public const double SeverityThreshold2 = 0.3;
    public const double SeverityThreshold3 = 0.4;
    public const double SeverityThreshold4 = 0.5;
    public const double SeverityThreshold5 = 0.6;
    public const double SeverityThreshold6 = 0.8;
    public const double SeverityThreshold7 = 1.0;

    /// <summary>
    /// Calculates severity level based on OpenAI score (0-1).
    /// </summary>
    /// <param name="score">The OpenAI score.</param>
    /// <returns>A severity level (0-7).</returns>
    public static int CalculateSeverity(double score)
    {
        if (score <= SeverityThreshold0) return 0;
        if (score <= SeverityThreshold1) return 1;
        if (score <= SeverityThreshold2) return 2;
        if (score <= SeverityThreshold3) return 3;
        if (score <= SeverityThreshold4) return 4;
        if (score <= SeverityThreshold5) return 5;
        if (score <= SeverityThreshold6) return 6;
        if (score <= SeverityThreshold7) return 7;
        return 0;
    }

    /// <summary>
    /// Calculates confidence score based on OpenAI score.
    /// </summary>
    /// <param name="score">The OpenAI score (0-1).</param>
    /// <returns>A confidence score (0-100).</returns>
    public static int CalculateConfidence(double score)
    {
        return (int)(score * 100);
    }

    /// <summary>
    /// Gets a description for a detected harm category.
    /// </summary>
    /// <param name="category">The harm category.</param>
    /// <param name="severity">The severity level.</param>
    /// <param name="context">Optional context for the description (e.g., "streaming content", "single content").</param>
    /// <returns>A description of the detection.</returns>
    public static string GetCategoryDescription(HarmCategory category, int severity, string context = "content")
    {
        var severityText = severity switch
        {
            <= 2 => "low",
            <= 4 => "medium",
            <= 6 => "high",
            _ => "very high"
        };

        return $"{category} {context} detected with {severityText} severity (level {severity}).";
    }

    /// <summary>
    /// Gets a recommendation for handling detected harmful content.
    /// </summary>
    /// <param name="category">The harm category.</param>
    /// <param name="severity">The severity level.</param>
    /// <param name="isStreaming">Whether this is for streaming content.</param>
    /// <returns>A recommendation string.</returns>
    public static string GetRecommendation(HarmCategory category, int severity, bool isStreaming = false)
    {
        var streamingPrefix = isStreaming ? "Streaming " : "";
        
        return category switch
        {
            HarmCategory.Hate => $"{streamingPrefix}content contains hate speech and should be {(isStreaming ? "terminated immediately" : "blocked or heavily moderated")}.",
            HarmCategory.Harassment => $"{streamingPrefix}content contains harassment and should be {(isStreaming ? "terminated immediately" : "blocked or heavily moderated")}.",
            HarmCategory.SelfHarm => $"{streamingPrefix}content contains self-harm references and requires {(isStreaming ? "immediate intervention" : "immediate attention")}.",
            HarmCategory.Sexual => $"{streamingPrefix}content contains sexually explicit material and should be {(isStreaming ? "blocked" : "restricted")}.",
            HarmCategory.Violence => $"{streamingPrefix}content contains violent material and should be {(isStreaming ? "terminated" : "age-restricted or blocked")}.",
            _ => $"{streamingPrefix}content contains potentially harmful material and should be reviewed."
        };
    }

    /// <summary>
    /// Calculates an overall risk score based on detected categories and scores.
    /// </summary>
    /// <param name="detectedCategories">The detected harm categories.</param>
    /// <param name="maxScore">The maximum score detected.</param>
    /// <returns>A risk score (0-100).</returns>
    public static int CalculateRiskScore(List<DetectedHarmCategory> detectedCategories, double maxScore)
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
}