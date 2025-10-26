using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AIChat.WebApi.Hubs;

namespace AIChat.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SignalRTestController : ControllerBase
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<SignalRTestController> _logger;

    public SignalRTestController(
        IHubContext<ChatHub> hubContext,
        ILogger<SignalRTestController> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Test SignalR hub connectivity
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new
        {
            Status = "SignalR hub is configured",
            Endpoint = "/chathub",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast test message to all connected clients
    /// </summary>
    [HttpPost("broadcast")]
    public async Task<IActionResult> BroadcastTest([FromBody] string message)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TestMessage", message);
            _logger.LogInformation("Broadcast test message sent: {Message}", message);
            return Ok(new { message = "Broadcast sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast test message");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}