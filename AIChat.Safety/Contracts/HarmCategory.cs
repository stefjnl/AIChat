namespace AIChat.Safety.Contracts;

/// <summary>
/// Defines categories of harmful content that can be detected by safety evaluators.
/// </summary>
public enum HarmCategory
{
    /// <summary>
    /// Content that expresses, incites, or promotes hate based on race, ethnicity, religion, gender, etc.
    /// </summary>
    Hate,

    /// <summary>
    /// Content that encourages or provides instructions for self-harm or suicide.
    /// </summary>
    SelfHarm,

    /// <summary>
    /// Sexually explicit content or content intended to be sexually gratifying.
    /// </summary>
    Sexual,

    /// <summary>
    /// Content that depicts or promotes violence, physical harm, or dangerous activities.
    /// </summary>
    Violence,

    /// <summary>
    /// Content that is sexually suggestive but not explicit.
    /// </summary>
    Suggestive,

    /// <summary>
    /// Content that contains profanity or vulgar language.
    /// </summary>
    Profanity,

    /// <summary>
    /// Content that contains personal identifiable information.
    /// </summary>
    PersonalData,

    /// <summary>
    /// Content that may be inappropriate for certain age groups.
    /// </summary>
    AgeInappropriate
}