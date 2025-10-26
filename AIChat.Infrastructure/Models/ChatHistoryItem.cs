namespace AIChat.Infrastructure.Models;

/// <summary>
/// Represents a chat history item with metadata for display and management
/// </summary>
public class ChatHistoryItem
{
    /// <summary>
    /// Unique thread identifier
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Auto-generated title from first user message or AI summary
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Timestamp when the thread was created
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the thread was last updated
    /// </summary>
    public required DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// Number of messages in the thread
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// First user message (used for title generation)
    /// </summary>
    public string? FirstUserMessage { get; set; }

    /// <summary>
    /// Last message preview (for future use)
    /// </summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>
    /// Provider used in this thread
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Whether this thread is currently active
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Request model for updating chat history metadata
/// </summary>
public class UpdateChatHistoryRequest
{
    public string? Title { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Request model for creating a new chat history item
/// </summary>
public class CreateChatHistoryRequest
{
    public required string ThreadId { get; set; }
    public required string Title { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public int MessageCount { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Response model for chat history operations
/// </summary>
public class ChatHistoryResponse
{
    public required List<ChatHistoryItem> Items { get; set; }
    public required int TotalCount { get; set; }
    public DateTime? LastSyncTime { get; set; }
}