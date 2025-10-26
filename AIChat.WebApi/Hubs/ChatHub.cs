using Microsoft.AspNetCore.SignalR;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIChat.Infrastructure.Storage;
using AIChat.Infrastructure.Models;
using AIChat.Shared.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Linq;

namespace AIChat.WebApi.Hubs;

/// <summary>
/// SignalR hub for real-time chat communication
/// </summary>
public class ChatHub : Hub
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThreadStorage _threadStorage;
    private readonly IChatHistoryStorage _chatHistoryStorage;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IServiceProvider serviceProvider,
        IThreadStorage threadStorage,
        IChatHistoryStorage chatHistoryStorage,
        ILogger<ChatHub> logger)
    {
        _serviceProvider = serviceProvider;
        _threadStorage = threadStorage;
        _chatHistoryStorage = chatHistoryStorage;
        _logger = logger;
    }

    /// <summary>
    /// Stream chat responses from the selected provider
    /// </summary>
    /// <param name="provider">Provider name (OpenRouter, NanoGPT, LMStudio)</param>
    /// <param name="message">User message</param>
    /// <param name="threadId">Optional thread ID for conversation continuity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of ChatChunk objects</returns>
    public async IAsyncEnumerable<ChatChunk> StreamChat(
        string provider,
        string message,
        string? threadId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "StreamChat started - Provider: {Provider}, ThreadId: {ThreadId}, ConnectionId: {ConnectionId}",
            provider, threadId ?? "new", Context.ConnectionId);

        // Validate input
        if (string.IsNullOrWhiteSpace(provider))
        {
            yield return new ChatChunk
            {
                Text = null,
                IsFinal = true,
                Error = "Provider name is required"
            };
            yield break;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            yield return new ChatChunk
            {
                Text = null,
                IsFinal = true,
                Error = "Message is required"
            };
            yield break;
        }

        AgentThread? thread = null;
        AIAgent? agent = null;
        Exception? streamException = null;
        UsageDetails? usage = null;

        // Resolve agent
        AIAgent? resolvedAgent = null;
        string? agentError = null;
        
        try
        {
            resolvedAgent = _serviceProvider.GetRequiredKeyedService<AIAgent>(provider);
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Provider not found: {Provider}", provider);
            agentError = $"Provider '{provider}' not found";
        }

        if (agentError != null)
        {
            yield return new ChatChunk
            {
                Text = null,
                IsFinal = true,
                Error = agentError
            };
            yield break;
        }

        agent = resolvedAgent!;

        // Load or create thread with conversation history
        threadId ??= _threadStorage.CreateNewThreadId();
        var (loadedThread, conversationHistory) = await LoadOrCreateThreadWithHistoryAsync(agent, threadId, cancellationToken);

        thread = loadedThread;
        _logger.LogDebug("Using thread: {ThreadId} with {MessageCount} messages", threadId, conversationHistory.Count);

        // Add the new user message to conversation history
        var userMessage = new ChatMessage(ChatRole.User, message);
        var userTimestamp = DateTime.UtcNow;
        conversationHistory.Add(userMessage);

        // Track timestamp for the new user message
        var newMessageTimestamps = new Dictionary<int, DateTime>
        {
            { conversationHistory.Count - 1, userTimestamp }
        };

        // Stream response from agent using full conversation history
        var chunks = new List<ChatChunk>();
        string fullResponse = "";
        
        try
        {
            await foreach (var update in agent.RunStreamingAsync(conversationHistory.ToArray()))
            {
                // Check for usage information
                var usageContent = update.Contents?.OfType<UsageContent>().FirstOrDefault();
                if (usageContent != null)
                {
                    usage = usageContent.Details;
                    _logger.LogDebug(
                        "Token usage - Input: {Input}, Output: {Output}, Total: {Total}",
                        usage.InputTokenCount,
                        usage.OutputTokenCount,
                        usage.TotalTokenCount);
                }

                // Collect text chunks
                if (!string.IsNullOrEmpty(update.Text))
                {
                    chunks.Add(new ChatChunk
                    {
                        Text = update.Text,
                        IsFinal = false,
                        ThreadId = threadId
                    });
                    fullResponse += update.Text;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "StreamChat cancelled by client - Provider: {Provider}, ThreadId: {ThreadId}",
                provider, threadId);
            streamException = new OperationCanceledException("Request cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in StreamChat - Provider: {Provider}, ThreadId: {ThreadId}",
                provider, threadId);
            streamException = ex;
        }

        // Handle exceptions
        if (streamException != null)
        {
            if (streamException is OperationCanceledException)
            {
                yield return new ChatChunk
                {
                    Text = null,
                    IsFinal = true,
                    ThreadId = threadId,
                    Error = "Request cancelled"
                };
            }
            else
            {
                yield return new ChatChunk
                {
                    Text = null,
                    IsFinal = true,
                    ThreadId = threadId,
                    Error = $"Error: {streamException.Message}"
                };
            }
            yield break;
        }

        // Add the assistant's response to conversation history
        if (!string.IsNullOrWhiteSpace(fullResponse))
        {
            var assistantMessage = new ChatMessage(ChatRole.Assistant, fullResponse);
            var assistantTimestamp = DateTime.UtcNow;
            conversationHistory.Add(assistantMessage);
            newMessageTimestamps[conversationHistory.Count - 1] = assistantTimestamp;
        }

        // Yield all collected chunks
        foreach (var chunk in chunks)
        {
            yield return chunk;
        }

        // Save thread with updated conversation history
        try
        {
            await SaveThreadWithHistoryAsync(agent, thread, threadId, conversationHistory, newMessageTimestamps, cancellationToken);
            
            // Update chat history metadata and notify clients
            await UpdateChatHistoryAsync(threadId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save thread or update chat history: {ThreadId}", threadId);
        }

        // Send final chunk with usage and thread ID
        yield return new ChatChunk
        {
            Text = null,
            IsFinal = true,
            ThreadId = threadId,
            Usage = usage != null ? new TokenUsage
            {
                InputTokens = (int)(usage.InputTokenCount ?? 0),
                OutputTokens = (int)(usage.OutputTokenCount ?? 0),
                TotalTokens = (int)(usage.TotalTokenCount ?? 0)
            } : null
        };

        _logger.LogInformation(
            "StreamChat completed successfully - Provider: {Provider}, ThreadId: {ThreadId}",
            provider, threadId);
    }

    /// <summary>
    /// Load existing thread with conversation history or create new one
    /// </summary>
    private async Task<(AgentThread thread, List<ChatMessage> conversationHistory)> LoadOrCreateThreadWithHistoryAsync(
        AIAgent agent,
        string threadId,
        CancellationToken cancellationToken)
    {
        try
        {
            var threadData = await _threadStorage.LoadThreadAsync(threadId, cancellationToken);

            if (threadData.HasValue)
            {
                _logger.LogDebug("Loading existing thread: {ThreadId}", threadId);
                
                // Load conversation history from thread data
                var conversationHistory = LoadConversationHistory(threadData.Value);
                
                // Create a new thread since AgentThread doesn't expose messages directly
                return (agent.GetNewThread(), conversationHistory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load thread {ThreadId}, creating new one", threadId);
        }

        _logger.LogDebug("Creating new thread: {ThreadId}", threadId);
        return (agent.GetNewThread(), new List<ChatMessage>());
    }

    /// <summary>
    /// Load conversation history from thread data
    /// </summary>
    private List<ChatMessage> LoadConversationHistory(JsonElement threadData)
    {
        var messages = new List<ChatMessage>();
        
        try
        {
            if (threadData.TryGetProperty("messages", out var messagesProperty) && 
                messagesProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var msg in messagesProperty.EnumerateArray())
                {
                    if (msg.TryGetProperty("role", out var roleProperty) &&
                        msg.TryGetProperty("content", out var contentProperty) &&
                        roleProperty.ValueKind == JsonValueKind.String &&
                        contentProperty.ValueKind == JsonValueKind.String)
                    {
                        var role = roleProperty.GetString();
                        var content = contentProperty.GetString();
                        
                        if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(content))
                        {
                            var chatRole = role.ToLowerInvariant() switch
                            {
                                "user" => ChatRole.User,
                                "assistant" => ChatRole.Assistant,
                                "system" => ChatRole.System,
                                _ => ChatRole.User
                            };
                            
                            messages.Add(new ChatMessage(chatRole, content));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading conversation history from thread data");
        }
        
        return messages;
    }

    /// <summary>
    /// Save thread with conversation history to persistent storage
    /// </summary>
    private async Task SaveThreadWithHistoryAsync(
        AIAgent agent,
        AgentThread thread,
        string threadId,
        List<ChatMessage> conversationHistory,
        Dictionary<int, DateTime> newMessageTimestamps,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load existing thread data to preserve timestamps
            var existingData = await _threadStorage.LoadThreadAsync(threadId, cancellationToken);
            var existingTimestamps = new Dictionary<int, DateTime>();
            
            if (existingData.HasValue && existingData.Value.TryGetProperty("messages", out var existingMessages))
            {
                var index = 0;
                foreach (var msg in existingMessages.EnumerateArray())
                {
                    if (msg.TryGetProperty("timestamp", out var timestampProperty) && 
                        timestampProperty.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(timestampProperty.GetString(), out var timestamp))
                    {
                        existingTimestamps[index] = timestamp;
                    }
                    index++;
                }
            }

            // Create thread data structure with conversation history
            var now = DateTime.UtcNow;
            var threadData = new
            {
                threadId = threadId,
                created = DateTime.UtcNow,
                provider = agent.Name,
                messages = conversationHistory.Select((msg, index) => new
                {
                    role = msg.Role.ToString().ToLowerInvariant(),
                    content = msg.Text,
                    timestamp = existingTimestamps.TryGetValue(index, out var existingTimestamp) 
                        ? existingTimestamp 
                        : newMessageTimestamps.TryGetValue(index, out var newTimestamp)
                            ? newTimestamp
                            : now
                }).ToArray()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(threadData);
            var jsonElement = System.Text.Json.JsonDocument.Parse(json).RootElement;
            
            await _threadStorage.SaveThreadAsync(threadId, jsonElement, cancellationToken);
            _logger.LogDebug("Thread saved successfully: {ThreadId} with {MessageCount} messages", threadId, conversationHistory.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save thread: {ThreadId}", threadId);
            // Don't throw - we don't want to fail the response if save fails
        }
    }

    /// <summary>
    /// Update chat history and notify all connected clients
    /// </summary>
    private async Task UpdateChatHistoryAsync(string threadId, CancellationToken cancellationToken)
    {
        try
        {
            // Update thread metadata in chat history
            var threadData = await _threadStorage.LoadThreadAsync(threadId, cancellationToken);
            if (threadData.HasValue)
            {
                await _chatHistoryStorage.UpdateThreadMetadataAsync(threadId, threadData.Value, cancellationToken);
            }
            
            // Notify all connected clients about the chat history update
            var historyItem = await _chatHistoryStorage.GetChatHistoryItemAsync(threadId, cancellationToken);
            if (historyItem != null)
            {
                await Clients.All.SendAsync("ChatHistoryUpdated", historyItem, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating chat history for thread: {ThreadId}", threadId);
        }
    }

    /// <summary>
    /// Get chat history for the connected client
    /// </summary>
    public async Task<List<AIChat.Infrastructure.Models.ChatHistoryItem>> GetChatHistory()
    {
        try
        {
            return await _chatHistoryStorage.GetChatHistoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history for client: {ConnectionId}", Context.ConnectionId);
            return new List<AIChat.Infrastructure.Models.ChatHistoryItem>();
        }
    }

    /// <summary>
    /// Set active thread for the connected client
    /// </summary>
    public async Task<bool> SetActiveThread(string threadId)
    {
        try
        {
            await _chatHistoryStorage.SetActiveThreadAsync(threadId);
            await Clients.All.SendAsync("ActiveThreadChanged", threadId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active thread: {ThreadId}", threadId);
            return false;
        }
    }

    /// <summary>
    /// Delete a chat history item
    /// </summary>
    public async Task<bool> DeleteChatHistoryItem(string threadId)
    {
        try
        {
            var success = await _chatHistoryStorage.DeleteChatHistoryItemAsync(threadId);
            if (success)
            {
                await Clients.All.SendAsync("ChatHistoryItemDeleted", threadId);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chat history item: {ThreadId}", threadId);
            return false;
        }
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception,
                "Client disconnected with error: {ConnectionId}",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}