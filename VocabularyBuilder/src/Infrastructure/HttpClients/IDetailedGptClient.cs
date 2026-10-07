namespace VocabularyBuilder.Infrastructure.HttpClients;

/// <summary>A model's answer together with what the call cost and how it went.</summary>
public record GptCompletion(
    string? Content,
    string? Model = null,
    int? StatusCode = null,
    string? Error = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    bool IsMock = false);

/// <summary>
/// A model client that can report more than the answer. Implemented by the clients this
/// project owns, so that the call log gets tokens and status; anything else is still logged,
/// with the answer alone.
/// </summary>
public interface IDetailedGptClient
{
    Task<GptCompletion> CompleteAsync(string prompt);
}
