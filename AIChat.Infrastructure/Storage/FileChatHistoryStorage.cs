using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using AIChat.Infrastructure.Models;

namespace AIChat.Infrastructure.Storage;

/// <summary>
/// File-based implementation of chat history storage
/// </summary>
public class FileChatHistoryStorage : IChatHistoryStorage
{
    private readonly string _basePath;
    private readonly string _historyFilePath;
    private readonly IThreadStorage _threadStorage;
    private readonly ILogger<FileChatHistoryStorage> _logger;

    public FileChatHistoryStorage(
        IOptions<StorageOptions> options,
        IThreadStorage threadStorage,
        ILogger<FileChatHistoryStorage> logger)
    {
        _basePath = options.Value.ThreadsPath;
        _historyFilePath = Path.Combine(_basePath, "chat_history.json");
        _threadStorage = threadStorage;
        _logger = logger;
        
        Directory.CreateDirectory(_basePath);
    }

    public async Task<List<ChatHistoryItem>> GetChatHistoryAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_historyFilePath))
                return new List<ChatHistoryItem>();

            var json = await File.ReadAllTextAsync(_historyFilePath, ct);
            var history = JsonSerializer.Deserialize<List<ChatHistoryItem>>(json) ?? new List<ChatHistoryItem>();
            
            // Sort by last updated (descending) and ensure proper ordering
            return history
                .OrderByDescending(h => h.LastUpdatedAt)
                .ThenByDescending(h => h.CreatedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading chat history");
            return new List<ChatHistoryItem>();
        }
    }

    public async Task<ChatHistoryItem?> GetChatHistoryItemAsync(string threadId, CancellationToken ct = default)
    {
        var history = await GetChatHistoryAsync(ct);
        return history.FirstOrDefault(h => h.ThreadId == threadId);
    }

    public async Task SaveChatHistoryItemAsync(ChatHistoryItem item, CancellationToken ct = default)
    {
        try
        {
            var history = await GetChatHistoryAsync(ct);
            
            // Remove existing item if it exists
            history.RemoveAll(h => h.ThreadId == item.ThreadId);
            
            // Add the updated item
            history.Add(item);
            
            // Save back to file
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_historyFilePath, json, ct);
            
            _logger.LogInformation("Saved chat history item for thread: {ThreadId}", item.ThreadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving chat history item for thread: {ThreadId}", item.ThreadId);
            throw;
        }
    }

    public async Task<bool> DeleteChatHistoryItemAsync(string threadId, CancellationToken ct = default)
    {
        try
        {
            var history = await GetChatHistoryAsync(ct);
            var item = history.FirstOrDefault(h => h.ThreadId == threadId);
            
            if (item == null)
                return false;

            // Remove from history
            history.Remove(item);
            
            // Save updated history
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_historyFilePath, json, ct);
            
            // Delete associated thread data
            var threadFilePath = Path.Combine(_basePath, $"{threadId}.json");
            if (File.Exists(threadFilePath))
            {
                File.Delete(threadFilePath);
            }
            
            _logger.LogInformation("Deleted chat history item and thread data for: {ThreadId}", threadId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chat history item for thread: {ThreadId}", threadId);
            return false;
        }
    }

    public string GenerateAutoTitle(string firstUserMessage)
    {
        if (string.IsNullOrWhiteSpace(firstUserMessage))
            return "New Conversation";

        // Clean the message
        var cleanMessage = firstUserMessage.Trim();
        
        // If it's very short, use it as-is
        if (cleanMessage.Length <= 30)
            return cleanMessage;

        // Take first 50 characters and add ellipsis for better context
        var title = cleanMessage.Substring(0, Math.Min(50, cleanMessage.Length)).Trim();
        
        // Ensure we don't cut off in the middle of a word
        var lastSpace = title.LastIndexOf(' ');
        if (lastSpace > 40)
            title = title.Substring(0, lastSpace);
        
        return title + "...";
    }

    public async Task SetActiveThreadAsync(string? threadId, CancellationToken ct = default)
    {
        try
        {
            var history = await GetChatHistoryAsync(ct);
            
            // Deactivate all threads
            foreach (var item in history)
            {
                item.IsActive = false;
            }
            
            // Activate the specified thread
            if (threadId != null)
            {
                var activeItem = history.FirstOrDefault(h => h.ThreadId == threadId);
                if (activeItem != null)
                {
                    activeItem.IsActive = true;
                    activeItem.LastUpdatedAt = DateTime.UtcNow;
                }
            }
            
            // Save updated history
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_historyFilePath, json, ct);
            
            _logger.LogInformation("Set active thread: {ThreadId}", threadId ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active thread: {ThreadId}", threadId ?? "none");
            throw;
        }
    }

    public async Task<string?> GetActiveThreadIdAsync(CancellationToken ct = default)
    {
        var history = await GetChatHistoryAsync(ct);
        return history.FirstOrDefault(h => h.IsActive)?.ThreadId;
    }

    public async Task UpdateThreadMetadataAsync(string threadId, JsonElement threadData, CancellationToken ct = default)
    {
        try
        {
            var item = await GetChatHistoryItemAsync(threadId, ct);
            if (item == null)
            {
                // Create new history item
                item = new ChatHistoryItem
                {
                    ThreadId = threadId,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow,
                    Title = "New Conversation",
                    MessageCount = 0
                };
            }

            // Extract metadata from thread data
            try
            {
                // Try to get message count from thread data
                if (threadData.TryGetProperty("messages", out var messagesProperty) && 
                    messagesProperty.ValueKind == JsonValueKind.Array)
                {
                    item.MessageCount = messagesProperty.GetArrayLength();
                }
                
                // Try to get first user message for title generation
                if (threadData.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var msg in msgs.EnumerateArray())
                    {
                        if (msg.TryGetProperty("role", out var role) && 
                            role.ValueKind == JsonValueKind.String &&
                            role.GetString() == "user" &&
                            msg.TryGetProperty("content", out var content) &&
                            content.ValueKind == JsonValueKind.String)
                        {
                            var firstMessage = content.GetString();
                            if (!string.IsNullOrEmpty(firstMessage) && string.IsNullOrEmpty(item.FirstUserMessage))
                            {
                                item.FirstUserMessage = firstMessage;
                                item.Title = GenerateAutoTitle(firstMessage);
                                break;
                            }
                        }
                    }
                }
                
                // Try to get provider information
                if (threadData.TryGetProperty("provider", out var provider) && 
                    provider.ValueKind == JsonValueKind.String)
                {
                    item.Provider = provider.GetString();
                }
            }
            catch (Exception parseEx)
            {
                _logger.LogWarning(parseEx, "Error parsing thread data for metadata extraction");
            }

            item.LastUpdatedAt = DateTime.UtcNow;
            await SaveChatHistoryItemAsync(item, ct);
            
            _logger.LogInformation("Updated thread metadata for: {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating thread metadata for: {ThreadId}", threadId);
            throw;
        }
    }
}