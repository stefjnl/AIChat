using System.Text.RegularExpressions;

namespace AIChat.WebApi.Services;

/// <summary>
/// Service for analyzing and processing text content
/// </summary>
public class TextAnalysisService
{
    private static readonly string[] TechnicalTerms = {
        "javascript", "python", "java", "c#", "react", "angular", "vue", "node", "express",
        "api", "database", "sql", "nosql", "mongodb", "mysql", "postgresql",
        "html", "css", "frontend", "backend", "fullstack", "devops", "docker",
        "algorithm", "data structure", "function", "class", "method", "variable",
        "bug", "error", "issue", "problem", "solution", "fix", "debug",
        "code", "programming", "development", "software", "application", "system",
        "test", "testing", "unit test", "integration", "deployment"
    };

    private static readonly string[] QuestionIndicators = {
        "?", "what", "how", "why", "when", "where", "which", "who",
        "can", "could", "would", "should", "is", "are", "do", "does"
    };

    private static readonly string[] TaskIndicators = {
        "help", "create", "write", "generate", "make", "build", "implement",
        "develop", "design", "explain", "show", "tell"
    };

    private static readonly string[] QuestionWords = {
        "what", "how", "why", "when", "where", "which", "who",
        "can", "could", "would", "should", "is", "are", "do", "does"
    };

    private static readonly string[] TaskVerbs = {
        "help", "create", "write", "generate", "make", "build", "implement",
        "develop", "design", "explain", "show", "tell"
    };

    /// <summary>
    /// Detect if the message is a question, request, or statement
    /// </summary>
    public string DetectMessageType(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "statement";

        var lowerMessage = message.ToLowerInvariant();

        // Check for questions
        if (message.Contains("?") || QuestionIndicators.Any(indicator =>
            lowerMessage.StartsWith(indicator + " ") || lowerMessage.Contains(" " + indicator + " ")))
        {
            return "question";
        }

        // Check for tasks/requests
        if (TaskIndicators.Any(indicator => lowerMessage.Contains(indicator)))
        {
            return "task";
        }

        return "statement";
    }

    /// <summary>
    /// Extract important keywords from the messages
    /// </summary>
    public List<string> ExtractKeywords(string userText, string aiText)
    {
        var allText = $"{userText} {aiText}".ToLowerInvariant();

        var words = Regex.Matches(allText, @"\b[a-z]{3,}\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

        var keywords = new List<string>();

        // Add technical terms found in the text
        keywords.AddRange(TechnicalTerms.Where(term => allText.Contains(term)));

        // Add capitalized words (likely proper nouns)
        var capitalizedWords = Regex.Matches(
            $"{userText} {aiText}", @"\b[A-Z][a-z]+\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToList();

        keywords.AddRange(capitalizedWords);

        // Return unique keywords, limited to 5
        return keywords.Distinct().Take(5).ToList();
    }

    /// <summary>
    /// Check if a term is a technical term
    /// </summary>
    public bool IsTechnicalTerm(string term)
    {
        return TechnicalTerms.Contains(term.ToLowerInvariant());
    }

    /// <summary>
    /// Extract the question part from a question message
    /// </summary>
    public string ExtractQuestion(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var question = message.Trim();

        foreach (var word in QuestionWords)
        {
            var regex = new Regex($"^{word}\\s+(is|are)?\\s+",
                RegexOptions.IgnoreCase);
            question = regex.Replace(question, "");
        }

        // Remove question mark and clean up
        question = question.Replace("?", "").Trim();

        return !string.IsNullOrEmpty(question) && question.Length > 0 ? question : string.Empty;
    }

    /// <summary>
    /// Extract the task/action from a task message
    /// </summary>
    public string? ExtractTask(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var lowerMessage = message.ToLowerInvariant();

        foreach (var verb in TaskVerbs)
        {
            if (lowerMessage.Contains(verb))
            {
                var regex = new Regex($"{verb}\\s+(.+)",
                    RegexOptions.IgnoreCase);
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
    /// Capitalize words properly
    /// </summary>
    public string CapitalizeWords(string str)
    {
        if (string.IsNullOrEmpty(str))
            return string.Empty;

        return Regex.Replace(str, @"\b\w", m => m.Value.ToUpperInvariant());
    }
}