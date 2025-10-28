namespace AIChat.Safety.Contracts;

/// <summary>
/// Represents the result of a safety evaluation for text content.
/// </summary>
public class SafetyEvaluationResult
{
    /// <summary>
    /// Gets or sets whether the evaluated content is considered safe.
    /// </summary>
    public bool IsSafe { get; set; }

    /// <summary>
    /// Gets or sets the list of detected harm categories with their severity levels.
    /// </summary>
    public List<DetectedHarmCategory> DetectedCategories { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall risk score (0-100, where higher is more risky).
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// Gets or sets the evaluation metadata including provider information and processing time.
    /// </summary>
    public EvaluationMetadata? Metadata { get; set; }

    /// <summary>
    /// Gets or sets recommendations for handling the content.
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Gets a pre-configured safe evaluation result.
    /// </summary>
    public static SafetyEvaluationResult Safe => new()
    {
        IsSafe = true,
        RiskScore = 0,
        Recommendations = { "Content is safe to proceed." }
    };

    /// <summary>
    /// Gets a pre-configured unsafe evaluation result with the specified harm category.
    /// </summary>
    /// <param name="category">The harm category that was detected.</param>
    /// <param name="severity">The severity level (0-7).</param>
    /// <returns>An unsafe evaluation result.</returns>
    public static SafetyEvaluationResult Unsafe(HarmCategory category, int severity)
    {
        return new SafetyEvaluationResult
        {
            IsSafe = false,
            RiskScore = severity * 10,
            DetectedCategories = { new DetectedHarmCategory { Category = category, Severity = severity } },
            Recommendations = { $"Content blocked due to {category} violation (severity: {severity})." }
        };
    }
}

/// <summary>
/// Represents a detected harm category with its severity and additional details.
/// </summary>
public class DetectedHarmCategory
{
    /// <summary>
    /// Gets or sets the category of harm that was detected.
    /// </summary>
    public HarmCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the severity level (0-7, where 7 is most severe).
    /// </summary>
    public int Severity { get; set; }

    /// <summary>
    /// Gets or sets a description of why this category was triggered.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the confidence score (0-100) for this detection.
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Gets or sets the specific text segments that triggered this detection.
    /// </summary>
    public List<string> TriggeringSegments { get; set; } = new();
}

/// <summary>
/// Metadata about the safety evaluation process.
/// </summary>
public class EvaluationMetadata
{
    /// <summary>
    /// Gets or sets the name of the safety provider that performed the evaluation.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the provider's API or model.
    /// </summary>
    public string? ProviderVersion { get; set; }

    /// <summary>
    /// Gets or sets the time taken to perform the evaluation in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the evaluation was performed.
    /// </summary>
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the request ID for tracking purposes.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific metadata.
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}