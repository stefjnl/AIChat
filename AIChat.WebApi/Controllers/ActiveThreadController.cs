using Microsoft.AspNetCore.Mvc;
using AIChat.Infrastructure.Storage;
using AIChat.Infrastructure.Models;

namespace AIChat.WebApi.Controllers;

/// <summary>
/// API controller for managing active threads
/// </summary>
[ApiController]
[Route("api/active-thread")]
public class ActiveThreadController : ControllerBase
{
    private readonly IChatHistoryStorage _chatHistoryStorage;
    private readonly IThreadStorage _threadStorage;
    private readonly ILogger<ActiveThreadController> _logger;

    public ActiveThreadController(
        IChatHistoryStorage chatHistoryStorage,
        IThreadStorage threadStorage,
        ILogger<ActiveThreadController> logger)
    {
        _chatHistoryStorage = chatHistoryStorage;
        _threadStorage = threadStorage;
        _logger = logger;
    }

    /// <summary>
    /// Set the active thread
    /// </summary>
    [HttpPost("{threadId}")]
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
    [HttpGet]
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
}