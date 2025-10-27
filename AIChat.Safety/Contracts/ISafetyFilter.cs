namespace AIChat.Safety.Contracts;

/// <summary>
/// Defines the contract for safety filters that can modify or sanitize content.
/// </summary>
public interface ISafetyFilter
{
    /// <summary>
    /// Filters and sanitizes text content to remove or mask harmful content.
    /// </summary>
    /// <param name="text">The text content to filter.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A filtered text result with information about what was modified.</returns>
    Task<FilteredTextResult> FilterTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of the filter provider.
    /// </summary>
    /// <returns>The provider name.</returns>
    string GetProviderName();
}

/// <summary>
/// Represents the result of text filtering operations.
/// </summary>
public class FilteredTextResult
{
    /// <summary>
    /// Gets or sets the filtered text content.
    /// </summary>
    public string FilteredText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether any content was filtered.
    /// </summary>
    public bool WasFiltered { get; set; }

    /// <summary>
    /// Gets or sets the list of filtering actions that were applied.
    /// </summary>
    public List<FilteringAction> AppliedActions { get; set; } = new();

    /// <summary>
    /// Gets or sets the original unfiltered text.
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single filtering action that was applied to text.
/// </summary>
public class FilteringAction
{
    /// <summary>
    /// Gets or sets the type of filtering action that was applied.
    /// </summary>
    public FilterActionType Action { get; set; }

    /// <summary>
    /// Gets or sets the harm category that triggered the filtering.
    /// </summary>
    public HarmCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the original text segment that was filtered.
    /// </summary>
    public string OriginalSegment { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the replacement text that was used.
    /// </summary>
    public string Replacement { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start position of the filtered segment in the original text.
    /// </summary>
    public int StartPosition { get; set; }

    /// <summary>
    /// Gets or sets the length of the filtered segment.
    /// </summary>
    public int Length { get; set; }
}

/// <summary>
/// Defines the types of filtering actions that can be applied.
/// </summary>
public enum FilterActionType
{
    /// <summary>
    /// The content was completely removed.
    /// </summary>
    Remove,

    /// <summary>
    /// The content was replaced with placeholder text.
    /// </summary>
    Replace,

    /// <summary>
    /// The content was masked with asterisks or other characters.
    /// </summary>
    Mask,

    /// <summary>
    /// The content was redacted with a generic message.
    /// </summary>
    Redact
}