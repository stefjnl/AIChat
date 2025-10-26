using Microsoft.AspNetCore.Mvc;
using AIChat.Infrastructure.Storage;
using AIChat.Infrastructure.Models;

namespace AIChat.WebApi.Controllers;

/// <summary>
/// API controller for managing chat history
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatHistoryController : ControllerBase
{
    private readonly IChatHistoryStorage _chatHistoryStorage;
    private readonly IThreadStorage _threadStorage;
    private readonly ILogger<ChatHistoryController> _logger;

    public ChatHistoryController(
        IChatHistoryStorage chatHistoryStorage,
        IThreadStorage threadStorage,
        ILogger<ChatHistoryController> logger)
    {
        _chatHistoryStorage = chatHistoryStorage;
        _threadStorage = threadStorage;
        _logger = logger;
    }

    /// <summary>
    /// Get all chat history items ordered by last updated (descending)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetChatHistory()
    {
        try
        {
            var history = await _chatHistoryStorage.GetChatHistoryAsync();
            var response = new ChatHistoryResponse
            {
                Items = history,
                TotalCount = history.Count,
                LastSyncTime = DateTime.UtcNow
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history");
            return StatusCode(500, new { error = "Failed to retrieve chat history" });
        }
    }

    /// <summary>
    /// Get a specific chat history item by thread ID
    /// </summary>
    [HttpGet("{threadId}")]
    public async Task<IActionResult> GetChatHistoryItem(string threadId)
    {
        try
        {
            var item = await _chatHistoryStorage.GetChatHistoryItemAsync(threadId);
            if (item == null)
            {
                return NotFound(new { error = "Chat history item not found" });
            }
            
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history item: {ThreadId}", threadId);
            return StatusCode(500, new { error = "Failed to retrieve chat history item" });
        }
    }

    /// <summary>
    /// Update chat history item (title, active status, etc.)
    /// </summary>
    [HttpPut("{threadId}")]
    public async Task<IActionResult> UpdateChatHistoryItem(string threadId, [FromBody] UpdateChatHistoryRequest request)
    {
        try
        {
            var item = await _chatHistoryStorage.GetChatHistoryItemAsync(threadId);
            if (item == null)
            {
                return NotFound(new { error = "Chat history item not found" });
            }

            // Update title if provided
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                item.Title = request.Title.Trim();
            }

            // Update active status if provided
            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value)
                {
                    await _chatHistoryStorage.SetActiveThreadAsync(threadId);
                }
                else
                {
                    item.IsActive = false;
                    await _chatHistoryStorage.SaveChatHistoryItemAsync(item);
                }
            }

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating chat history item: {ThreadId}", threadId);
            return StatusCode(500, new { error = "Failed to update chat history item" });
        }
    }

    /// <summary>
    /// Delete a chat history item and associated thread data
    /// </summary>
    [HttpDelete("{threadId}")]
    public async Task<IActionResult> DeleteChatHistoryItem(string threadId)
    {
        try
        {
            var success = await _chatHistoryStorage.DeleteChatHistoryItemAsync(threadId);
            if (!success)
            {
                return NotFound(new { error = "Chat history item not found" });
            }
            
            return Ok(new { message = "Chat history item deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chat history item: {ThreadId}", threadId);
            return StatusCode(500, new { error = "Failed to delete chat history item" });
        }
    }

    /// <summary>
    /// Set the active thread
    /// </summary>
    [HttpPost("active/{threadId}")]
    public async Task<IActionResult> SetActiveThread(string threadId)
    {
        try
        {
            // Verify thread exists
            var exists = await _threadStorage.ThreadExistsAsync(threadId);
            if (!exists)
            {
                return NotFound(new { error = "Thread not found" });
            }

            await _chatHistoryStorage.SetActiveThreadAsync(threadId);
            
            var item = await _chatHistoryStorage.GetChatHistoryItemAsync(threadId);
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active thread: {ThreadId}", threadId);
            return StatusCode(500, new { error = "Failed to set active thread" });
        }
    }

    /// <summary>
    /// Get the currently active thread
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveThread()
    {
        try
        {
            var activeThreadId = await _chatHistoryStorage.GetActiveThreadIdAsync();
            if (activeThreadId == null)
            {
                return Ok(new { threadId = (string?)null });
            }

            var item = await _chatHistoryStorage.GetChatHistoryItemAsync(activeThreadId);
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active thread");
            return StatusCode(500, new { error = "Failed to retrieve active thread" });
        }
    }

    /// <summary>
    /// Generate an auto-title from a message
    /// </summary>
    [HttpPost("generate-title")]
    public IActionResult GenerateAutoTitle([FromBody] string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest(new { error = "Message is required" });
            }

            var title = _chatHistoryStorage.GenerateAutoTitle(message);
            return Ok(new { title });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating auto-title");
            return StatusCode(500, new { error = "Failed to generate auto-title" });
        }
    }

    /// <summary>
    /// Update thread metadata from thread data
    /// </summary>
    [HttpPost("{threadId}/metadata")]
    public async Task<IActionResult> UpdateThreadMetadata(string threadId)
    {
        try
        {
            // Get thread data
            var threadData = await _threadStorage.LoadThreadAsync(threadId);
            if (threadData == null)
            {
                return NotFound(new { error = "Thread not found" });
            }

            await _chatHistoryStorage.UpdateThreadMetadataAsync(threadId, threadData.Value);
            
            var item = await _chatHistoryStorage.GetChatHistoryItemAsync(threadId);
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating thread metadata: {ThreadId}", threadId);
            return StatusCode(500, new { error = "Failed to update thread metadata" });
        }
    }
}