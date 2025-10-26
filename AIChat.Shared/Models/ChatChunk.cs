namespace AIChat.Shared.Models;

public class ChatChunk
{
    public string? Text { get; set; }
    public TokenUsage? Usage { get; set; }
    public bool IsFinal { get; set; }
}

public class TokenUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
}