using Microsoft.AspNetCore.Mvc;
using AIChat.Infrastructure.Storage;
using AIChat.Infrastructure.Models;
using AIChat.WebApi.Models;

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
    /// Generate an auto-title from a message with enhanced context
    /// </summary>
    [HttpPost("generate-title")]
    public IActionResult GenerateAutoTitle([FromBody] GenerateTitleRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserMessage))
            {
                return BadRequest(new { error = "User message is required" });
            }

            string title;
            
            // If we have enhanced context, use the improved generation
            if (request.Context != null)
            {
                title = GenerateTitleFromContext(request);
            }
            else
            {
                // Fallback to simple generation
                title = _chatHistoryStorage.GenerateAutoTitle(request.UserMessage);
            }

            return Ok(new { title });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating auto-title");
            return StatusCode(500, new { error = "Failed to generate auto-title" });
        }
    }

    /// <summary>
    /// Generate title from enhanced context
    /// </summary>
    private string GenerateTitleFromContext(GenerateTitleRequest request)
    {
        var context = request.Context;
        var userMessage = request.UserMessage;
        var aiResponse = request.AiResponse;
        var currentTitle = request.CurrentTitle;

        // If we have a current title from client-side generation, only improve it if we can do better
        if (!string.IsNullOrEmpty(currentTitle) && IsTitleMeaningful(currentTitle))
        {
            // Only improve if we have substantial additional context
            if (!string.IsNullOrEmpty(aiResponse) && aiResponse.Length > 100)
            {
                var improvedTitle = GenerateTitleWithFullContext(userMessage, aiResponse ?? string.Empty, context ?? new TitleContext());
                // Only use the improved title if it's significantly different and better
                if (!string.IsNullOrEmpty(improvedTitle) &&
                    improvedTitle.Length > currentTitle.Length &&
                    improvedTitle != currentTitle)
                {
                    return improvedTitle;
                }
            }
            return currentTitle;
        }

        // Generate new title with full context
        return GenerateTitleWithFullContext(userMessage, aiResponse ?? string.Empty, context ?? new TitleContext());
    }

    /// <summary>
    /// Generate title using both user message and AI response
    /// </summary>
    private string GenerateTitleWithFullContext(string userMessage, string aiResponse, TitleContext context)
    {
        try
        {
            // If we have context from client, use it
            if (context != null)
            {
                return GenerateTitleFromContextData(context);
            }

            // Otherwise, extract our own context
            var extractedContext = ExtractContextFromMessages(userMessage, aiResponse);
            return GenerateTitleFromContextData(extractedContext);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating enhanced title, falling back to simple generation");
            return _chatHistoryStorage.GenerateAutoTitle(userMessage);
        }
    }

    /// <summary>
    /// Generate title from pre-extracted context data
    /// </summary>
    private string GenerateTitleFromContextData(TitleContext context)
    {
        var messageType = context.MessageType?.ToLowerInvariant();
        var subject = context.Subject;
        var question = context.Question;
        var task = context.Task;
        var keywords = context.Keywords ?? new List<string>();

        // Use different patterns based on message type
        switch (messageType)
        {
            case "question":
                if (!string.IsNullOrEmpty(question) && !string.IsNullOrEmpty(subject))
                {
                    return $"{subject} Question";
                }
                if (!string.IsNullOrEmpty(subject))
                {
                    return $"{subject} Discussion";
                }
                break;

            case "task":
                if (!string.IsNullOrEmpty(task))
                {
                    var taskTitle = task.Length > 30 ? task.Substring(0, 30) + "..." : task;
                    return CapitalizeWords(taskTitle);
                }
                break;

            default:
                if (!string.IsNullOrEmpty(subject))
                {
                    return subject;
                }
                break;
        }

        // Fallback to keywords
        if (keywords.Count > 0)
        {
            // Prioritize technical terms
            var technicalKeywords = keywords.Where(k =>
                IsTechnicalTerm(k)).ToList();
            
            if (technicalKeywords.Count > 0)
            {
                return CapitalizeWords(technicalKeywords.First());
            }

            return CapitalizeWords(keywords.First());
        }

        // Final fallback
        return GenerateSimpleTitle(context.UserText ?? string.Empty);
    }

    /// <summary>
    /// Extract context from user and AI messages
    /// </summary>
    private TitleContext ExtractContextFromMessages(string userMessage, string aiResponse)
    {
        var userText = (userMessage ?? "").Trim();
        var aiText = (aiResponse ?? "").Trim();

        var context = new TitleContext
        {
            UserText = userText,
            AiText = aiText,
            MessageType = DetectMessageType(userText),
            Keywords = ExtractKeywords(userText, aiText)
        };

        context.Subject = IdentifySubject(userText, aiText, context.Keywords);
        context.Question = context.MessageType == "question" ? ExtractQuestion(userText) : null;
        context.Task = context.MessageType == "task" ? ExtractTask(userText) : null;

        return context;
    }

    /// <summary>
    /// Detect if the message is a question, request, or statement
    /// </summary>
    private string DetectMessageType(string message)
    {
        var questionIndicators = new[] { "?", "what", "how", "why", "when", "where", "which", "who", "can", "could", "would", "should", "is", "are", "do", "does" };
        var taskIndicators = new[] { "help", "create", "write", "generate", "make", "build", "implement", "develop", "design", "explain", "show", "tell" };

        var lowerMessage = message.ToLowerInvariant();

        // Check for questions
        if (message.Contains("?") || questionIndicators.Any(indicator =>
            lowerMessage.StartsWith(indicator + " ") || lowerMessage.Contains(" " + indicator + " ")))
        {
            return "question";
        }

        // Check for tasks/requests
        if (taskIndicators.Any(indicator => lowerMessage.Contains(indicator)))
        {
            return "task";
        }

        return "statement";
    }

    /// <summary>
    /// Extract important keywords from the messages
    /// </summary>
    private List<string> ExtractKeywords(string userText, string aiText)
    {
        var allText = $"{userText} {aiText}".ToLowerInvariant();
        
        // Common technical terms
        var technicalTerms = new[] {
            "javascript", "python", "java", "c#", "react", "angular", "vue", "node", "express",
            "api", "database", "sql", "nosql", "mongodb", "mysql", "postgresql",
            "html", "css", "frontend", "backend", "fullstack", "devops", "docker",
            "algorithm", "data structure", "function", "class", "method", "variable",
            "bug", "error", "issue", "problem", "solution", "fix", "debug",
            "code", "programming", "development", "software", "application", "system",
            "test", "testing", "unit test", "integration", "deployment"
        };
        
        var words = System.Text.RegularExpressions.Regex.Matches(allText, @"\b[a-z]{3,}\b")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Value)
            .ToList();
        
        var keywords = new List<string>();
        
        // Add technical terms found in the text
        keywords.AddRange(technicalTerms.Where(term => allText.Contains(term)));
        
        // Add capitalized words (likely proper nouns)
        var capitalizedWords = System.Text.RegularExpressions.Regex.Matches(
            $"{userText} {aiText}", @"\b[A-Z][a-z]+\b")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Value)
            .ToList();
        
        keywords.AddRange(capitalizedWords);
        
        // Return unique keywords, limited to 5
        return keywords.Distinct().Take(5).ToList();
    }

    /// <summary>
    /// Identify the main subject/topic of the conversation
    /// </summary>
    private string IdentifySubject(string userText, string aiText, List<string> keywords)
    {
        // If we have technical keywords, use the first/most prominent one
        if (keywords.Count > 0)
        {
            // Prioritize technical terms
            var technicalKeywords = keywords.Where(k =>
                IsTechnicalTerm(k)).ToList();
            
            if (technicalKeywords.Count > 0)
            {
                return CapitalizeWords(technicalKeywords.First());
            }
            
            // Use the first keyword if no technical ones
            return CapitalizeWords(keywords.First());
        }
        
        // Try to extract from the first sentence
        var firstSentence = userText.Split('.')[0];
        var words = firstSentence.Split(' ').Where(w => w.Length > 3).ToList();
        
        if (words.Count > 0)
        {
            return CapitalizeWords(words.First());
        }
        
        return null;
    }

    /// <summary>
    /// Extract the question part from a question message
    /// </summary>
    private string ExtractQuestion(string message)
    {
        var questionWords = new[] { "what", "how", "why", "when", "where", "which", "who", "can", "could", "would", "should", "is", "are", "do", "does" };
        var question = message.Trim();
        
        foreach (var word in questionWords)
        {
            var regex = new System.Text.RegularExpressions.Regex($"^{word}\\s+(is|are)?\\s+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            question = regex.Replace(question, "");
        }
        
        // Remove question mark and clean up
        question = question.Replace("?", "").Trim();
        
        return !string.IsNullOrEmpty(question) ? question : string.Empty;
    }

    /// <summary>
    /// Extract the task/action from a task message
    /// </summary>
    private string ExtractTask(string message)
    {
        var taskVerbs = new[] { "help", "create", "write", "generate", "make", "build", "implement", "develop", "design", "explain", "show", "tell" };
        var lowerMessage = message.ToLowerInvariant();
        
        foreach (var verb in taskVerbs)
        {
            if (lowerMessage.Contains(verb))
            {
                var regex = new System.Text.RegularExpressions.Regex($"{verb}\\s+(.+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var match = regex.Match(message);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Check if a term is a technical term
    /// </summary>
    private bool IsTechnicalTerm(string term)
    {
        var technicalTerms = new[] {
            "javascript", "python", "java", "c#", "react", "angular", "vue", "node", "express",
            "api", "database", "sql", "nosql", "mongodb", "mysql", "postgresql",
            "html", "css", "frontend", "backend", "fullstack", "devops", "docker",
            "algorithm", "data structure", "function", "class", "method", "variable",
            "bug", "error", "issue", "problem", "solution", "fix", "debug",
            "code", "programming", "development", "software", "application", "system"
        };
        
        return technicalTerms.Contains(term.ToLowerInvariant());
    }

    /// <summary>
    /// Capitalize words properly
    /// </summary>
    private string CapitalizeWords(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
            
        return System.Text.RegularExpressions.Regex.Replace(str, @"\b\w", m => m.Value.ToUpperInvariant());
    }

    /// <summary>
    /// Generate a simple title from text
    /// </summary>
    private string GenerateSimpleTitle(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "New Conversation";
            
        var cleanMessage = text.Trim();
        
        // If it's very short, use it as-is
        if (cleanMessage.Length <= 30)
            return cleanMessage;

        // Take first 50 characters
        var title = cleanMessage.Substring(0, Math.Min(50, cleanMessage.Length)).Trim();
        
        // Ensure we don't cut off in the middle of a word
        var lastSpace = title.LastIndexOf(' ');
        if (lastSpace > 40)
            title = title.Substring(0, lastSpace);
        
        return title + "...";
    }

    /// <summary>
    /// Check if a title is meaningful (not too generic)
    /// </summary>
    private bool IsTitleMeaningful(string title)
    {
        var genericTerms = new[] { "discussion", "question", "help", "chat", "conversation", "talk" };
        var lowerTitle = title.ToLowerInvariant();
        
        // If it's just a generic term, it's not meaningful
        if (genericTerms.Contains(lowerTitle))
        {
            return false;
        }
        
        // If it contains a generic term but also specific content, it's meaningful
        if (genericTerms.Any(term => lowerTitle.Contains(term)))
        {
            // Check if there's more than just the generic term
            var words = title.Split(' ').Where(w => w.Length > 2).ToList();
            return words.Count > 1;
        }
        
        return true;
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