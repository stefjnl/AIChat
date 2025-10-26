using System.Text.Json.Serialization;

namespace AIChat.WebApi.Models;

/// <summary>
/// Request model for generating chat titles
/// </summary>
public class GenerateTitleRequest
{
    /// <summary>
    /// The user's message
    /// </summary>
    [JsonPropertyName("userMessage")]
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// The AI's response (optional, for better context)
    /// </summary>
    [JsonPropertyName("aiResponse")]
    public string? AiResponse { get; set; }

    /// <summary>
    /// Pre-extracted context from the client (optional)
    /// </summary>
    [JsonPropertyName("context")]
    public TitleContext? Context { get; set; }

    /// <summary>
    /// Currently generated title from client-side (optional)
    /// </summary>
    [JsonPropertyName("currentTitle")]
    public string? CurrentTitle { get; set; }
}

/// <summary>
/// Context information extracted from messages for title generation
/// </summary>
public class TitleContext
{
    /// <summary>
    /// Type of message: "question", "task", or "statement"
    /// </summary>
    [JsonPropertyName("messageType")]
    public string? MessageType { get; set; }

    /// <summary>
    /// Extracted keywords from the messages
    /// </summary>
    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; } = new List<string>();

    /// <summary>
    /// The main subject/topic of the conversation
    /// </summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>
    /// The question part (for question messages)
    /// </summary>
    [JsonPropertyName("question")]
    public string? Question { get; set; }

    /// <summary>
    /// The task/action (for task messages)
    /// </summary>
    [JsonPropertyName("task")]
    public string? Task { get; set; }

    /// <summary>
    /// The original user text
    /// </summary>
    [JsonPropertyName("userText")]
    public string? UserText { get; set; }

    /// <summary>
    /// The original AI response text
    /// </summary>
    [JsonPropertyName("aiText")]
    public string? AiText { get; set; }
}