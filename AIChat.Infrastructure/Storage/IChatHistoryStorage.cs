using AIChat.Infrastructure.Models;
using System.Text.Json;

namespace AIChat.Infrastructure.Storage;

/// <summary>
/// Interface for managing chat history metadata and operations
/// </summary>
public interface IChatHistoryStorage
{
    /// <summary>
    /// Get all chat history items ordered by last updated (descending)
    /// </summary>
    Task<List<ChatHistoryItem>> GetChatHistoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Get a specific chat history item by thread ID
    /// </summary>
    Task<ChatHistoryItem?> GetChatHistoryItemAsync(string threadId, CancellationToken ct = default);

    /// <summary>
    /// Create or update chat history metadata
    /// </summary>
    Task SaveChatHistoryItemAsync(ChatHistoryItem item, CancellationToken ct = default);

    /// <summary>
    /// Delete chat history item and associated thread data
    /// </summary>
    Task<bool> DeleteChatHistoryItemAsync(string threadId, CancellationToken ct = default);

    /// <summary>
    /// Generate an auto-title from the first user message
    /// </summary>
    string GenerateAutoTitle(string firstUserMessage);

    /// <summary>
    /// Update the active status of a chat history item
    /// </summary>
    Task SetActiveThreadAsync(string? threadId, CancellationToken ct = default);

    /// <summary>
    /// Get the currently active thread ID
    /// </summary>
    Task<string?> GetActiveThreadIdAsync(CancellationToken ct = default);

    /// <summary>
    /// Update thread metadata from chat content
    /// </summary>
    Task UpdateThreadMetadataAsync(string threadId, JsonElement threadData, CancellationToken ct = default);
}