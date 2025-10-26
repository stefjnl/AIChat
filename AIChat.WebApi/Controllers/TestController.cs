using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AIChat.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TestController> _logger;

    public TestController(
        IServiceProvider serviceProvider,
        ILogger<TestController> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Test agent resolution and basic functionality
    /// </summary>
    [HttpPost("agent/{providerName}")]
    public async Task<IActionResult> TestAgent(string providerName, [FromBody] string message)
    {
        try
        {
            // Resolve agent
            var agent = _serviceProvider.GetRequiredKeyedService<AIAgent>(providerName);
            
            // Create test message
            var chatMessage = new ChatMessage(ChatRole.User, message ?? "Hello, please respond with a short greeting.");
            
            // Run agent
            var response = await agent.RunAsync(chatMessage);
            
            return Ok(new
            {
                Provider = providerName,
                Message = message,
                Response = response.Text,
                TokenUsage = new
                {
                    // Note: Usage may not always be available depending on provider
                    response.Usage?.InputTokenCount,
                    response.Usage?.OutputTokenCount,
                    response.Usage?.TotalTokenCount
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Provider not found: {Provider}", providerName);
            return NotFound(new { message = $"Provider '{providerName}' not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing agent: {Provider}", providerName);
            return StatusCode(500, new { message = "Agent test failed", error = ex.Message });
        }
    }

    /// <summary>
    /// Test streaming with an agent
    /// </summary>
    [HttpPost("agent/{providerName}/stream")]
    public async Task TestAgentStream(string providerName, [FromBody] string message)
    {
        try
        {
            // Set SSE headers
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            var agent = _serviceProvider.GetRequiredKeyedService<AIAgent>(providerName);
            var chatMessage = new ChatMessage(ChatRole.User, message ?? "Count to 10 slowly.");

            await foreach (var update in agent.RunStreamingAsync(chatMessage))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    await Response.WriteAsync($"data: {update.Text}\n\n");
                    await Response.Body.FlushAsync();
                }
            }

            await Response.WriteAsync("data: [DONE]\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming test: {Provider}", providerName);
            await Response.WriteAsync($"data: ERROR: {ex.Message}\n\n");
        }
    }
}