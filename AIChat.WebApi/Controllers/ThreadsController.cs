using Microsoft.AspNetCore.Mvc;
using AIChat.Infrastructure.Storage;

namespace AIChat.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ThreadsController : ControllerBase
{
    private readonly IThreadStorage _threadStorage;
    private readonly ILogger<ThreadsController> _logger;

    public ThreadsController(
        IThreadStorage threadStorage,
        ILogger<ThreadsController> logger)
    {
        _threadStorage = threadStorage;
        _logger = logger;
    }

    /// <summary>
    /// Create a new thread ID
    /// </summary>
    [HttpPost("new")]
    public IActionResult CreateNewThread()
    {
        try
        {
            var threadId = _threadStorage.CreateNewThreadId();
            _logger.LogInformation("Created new thread: {ThreadId}", threadId);
            
            return Ok(new { threadId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new thread");
            return StatusCode(500, "Failed to create thread");
        }
    }

    /// <summary>
    /// Check if a thread exists
    /// </summary>
    [HttpGet("{threadId}/exists")]
    public async Task<IActionResult> CheckThreadExists(string threadId)
    {
        try
        {
            var exists = await _threadStorage.ThreadExistsAsync(threadId);
            return Ok(new { exists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking thread existence: {ThreadId}", threadId);
            return StatusCode(500, "Failed to check thread");
        }
    }

    /// <summary>
    /// Delete a thread
    /// </summary>
    [HttpDelete("{threadId}")]
    public async Task<IActionResult> DeleteThread(string threadId)
    {
        try
        {
            var exists = await _threadStorage.ThreadExistsAsync(threadId);
            if (!exists)
            {
                return NotFound(new { message = "Thread not found" });
            }

            // Note: Implement DeleteThreadAsync in IThreadStorage if needed
            _logger.LogInformation("Thread deletion requested: {ThreadId}", threadId);
            return Ok(new { message = "Thread deletion not implemented yet" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting thread: {ThreadId}", threadId);
            return StatusCode(500, "Failed to delete thread");
        }
    }

    /// <summary>
    /// List all thread IDs
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListThreads()
    {
        try
        {
            var threadIds = await _threadStorage.ListThreadIdsAsync();
            return Ok(new { threadIds = threadIds.ToArray() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing threads");
            return StatusCode(500, "Failed to list threads");
        }
    }

    /// <summary>
    /// Get thread data
    /// </summary>
    [HttpGet("{threadId}")]
    public async Task<IActionResult> GetThread(string threadId)
    {
        try
        {
            var exists = await _threadStorage.ThreadExistsAsync(threadId);
            if (!exists)
            {
                return NotFound(new { message = "Thread not found" });
            }

            var data = await _threadStorage.LoadThreadAsync(threadId);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting thread: {ThreadId}", threadId);
            return StatusCode(500, "Failed to get thread");
        }
    }
}