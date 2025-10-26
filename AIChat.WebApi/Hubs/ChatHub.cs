using Microsoft.AspNetCore.SignalR;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIChat.Infrastructure.Storage;
using AIChat.Shared.Models;
using System.Runtime.CompilerServices;

namespace AIChat.WebApi.Hubs;

/// <summary>
/// SignalR hub for real-time chat communication
/// </summary>
public class ChatHub : Hub
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThreadStorage _threadStorage;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IServiceProvider serviceProvider,
        IThreadStorage threadStorage,
        ILogger<ChatHub> logger)
    {
        _serviceProvider = serviceProvider;
        _threadStorage = threadStorage;
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

        // Load or create thread
        threadId ??= _threadStorage.CreateNewThreadId();
        thread = await LoadOrCreateThreadAsync(agent, threadId, cancellationToken);

        _logger.LogDebug("Using thread: {ThreadId}", threadId);

        // Stream response from agent
        var chunks = new List<ChatChunk>();
        
        try
        {
            // Create chat message
            var chatMessage = new ChatMessage(ChatRole.User, message);
            
            await foreach (var update in agent.RunStreamingAsync(chatMessage))
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

        // Yield all collected chunks
        foreach (var chunk in chunks)
        {
            yield return chunk;
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

        // Save thread after successful completion
        try
        {
            await SaveThreadAsync(agent, thread, threadId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save thread: {ThreadId}", threadId);
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
    /// Load existing thread or create new one
    /// </summary>
    private async Task<AgentThread> LoadOrCreateThreadAsync(
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
                // For now, create a new thread since we don't have serialization methods
                // TODO: Implement thread serialization/deserialization when available
                return agent.GetNewThread();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load thread {ThreadId}, creating new one", threadId);
        }

        _logger.LogDebug("Creating new thread: {ThreadId}", threadId);
        return agent.GetNewThread();
    }

    /// <summary>
    /// Save thread to persistent storage
    /// </summary>
    private async Task SaveThreadAsync(
        AIAgent agent,
        AgentThread thread,
        string threadId,
        CancellationToken cancellationToken)
    {
        try
        {
            // For now, we'll save a placeholder since we don't have serialization methods
            // TODO: Implement proper thread serialization when available
            var placeholderData = System.Text.Json.JsonDocument.Parse("{\"threadId\":\"" + threadId + "\",\"created\":\"" + DateTime.UtcNow + "\"}").RootElement;
            await _threadStorage.SaveThreadAsync(threadId, placeholderData, cancellationToken);
            _logger.LogDebug("Thread saved successfully: {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save thread: {ThreadId}", threadId);
            // Don't throw - we don't want to fail the response if save fails
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