namespace AIChat.Shared.Models;

public class ChatRequest
{
    public required string Provider { get; set; }
    public required string Message { get; set; }
    public string? ThreadId { get; set; }
}