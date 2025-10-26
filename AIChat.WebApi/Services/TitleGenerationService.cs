using AIChat.WebApi.Models;
using AIChat.Infrastructure.Storage;

namespace AIChat.WebApi.Services;

/// <summary>
/// Service for generating conversation titles
/// </summary>
public interface ITitleGenerator
{
    string GenerateTitle(GenerateTitleRequest request);
}

/// <summary>
/// Service for generating conversation titles with enhanced context analysis
/// </summary>
public class TitleGenerationService : ITitleGenerator
{
    private readonly TextAnalysisService _textAnalysis;
    private readonly IChatHistoryStorage _chatHistoryStorage;

    // Constants for title generation
    private const int MaxTaskTitleLength = 30;
    private const int MinTitleLengthForImprovement = 100;
    private const int MaxSimpleTitleLength = 50;
    private const int MinWordLengthForSubject = 3;
    private const int MaxKeywords = 5;
    private const int WordBreakThreshold = 40;

    public TitleGenerationService(
        TextAnalysisService textAnalysis,
        IChatHistoryStorage chatHistoryStorage)
    {
        _textAnalysis = textAnalysis;
        _chatHistoryStorage = chatHistoryStorage;
    }

    /// <summary>
    /// Generate an auto-title from a message with enhanced context
    /// </summary>
    public string GenerateTitle(GenerateTitleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserMessage))
        {
            throw new ArgumentException("User message is required", nameof(request.UserMessage));
        }

        // If we have enhanced context, use the improved generation
        if (request.Context != null)
        {
            return GenerateTitleFromContext(request);
        }
        else
        {
            // Fallback to simple generation
            return _chatHistoryStorage.GenerateAutoTitle(request.UserMessage);
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
            if (!string.IsNullOrEmpty(aiResponse) && aiResponse.Length > MinTitleLengthForImprovement)
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
        catch
        {
            // Log warning and fall back to simple generation
            // In a real implementation, you'd inject ILogger here
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
                    var taskTitle = task.Length > MaxTaskTitleLength ? task.Substring(0, MaxTaskTitleLength) + "..." : task;
                    return _textAnalysis.CapitalizeWords(taskTitle);
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
                _textAnalysis.IsTechnicalTerm(k)).ToList();

            if (technicalKeywords.Count > 0)
            {
                return _textAnalysis.CapitalizeWords(technicalKeywords.First());
            }

            return _textAnalysis.CapitalizeWords(keywords.First());
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
            MessageType = _textAnalysis.DetectMessageType(userText),
            Keywords = _textAnalysis.ExtractKeywords(userText, aiText)
        };

        context.Subject = IdentifySubject(userText, aiText, context.Keywords);
        context.Question = context.MessageType == "question" ? _textAnalysis.ExtractQuestion(userText) : null;
        context.Task = context.MessageType == "task" ? _textAnalysis.ExtractTask(userText) : null;

        return context;
    }

    /// <summary>
    /// Identify the main subject/topic of the conversation
    /// </summary>
    private string? IdentifySubject(string userText, string aiText, List<string> keywords)
    {
        // If we have technical keywords, use the first/most prominent one
        if (keywords.Count > 0)
        {
            // Prioritize technical terms
            var technicalKeywords = keywords.Where(k =>
                _textAnalysis.IsTechnicalTerm(k)).ToList();

            if (technicalKeywords.Count > 0)
            {
                return _textAnalysis.CapitalizeWords(technicalKeywords.First());
            }

            // Use the first keyword if no technical ones
            return _textAnalysis.CapitalizeWords(keywords.First());
        }

        // Try to extract from the first sentence
        var firstSentence = userText.Split('.')[0];
        var words = firstSentence.Split(' ').Where(w => w.Length > MinWordLengthForSubject).ToList();

        if (words.Count > 0)
        {
            return _textAnalysis.CapitalizeWords(words.First());
        }

        return null;
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
        if (cleanMessage.Length <= MaxTaskTitleLength)
            return cleanMessage;

        // Take first 50 characters
        var title = cleanMessage.Substring(0, Math.Min(MaxSimpleTitleLength, cleanMessage.Length)).Trim();

        // Ensure we don't cut off in the middle of a word
        var lastSpace = title.LastIndexOf(' ');
        if (lastSpace > WordBreakThreshold)
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
}