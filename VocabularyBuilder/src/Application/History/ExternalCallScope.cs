using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.History;

/// <summary>
/// What the outbound requests made from here on are for, and on whose behalf.
/// </summary>
/// <remarks>
/// The clients that leave the machine know a prompt or a URL and nothing else - not that the
/// prompt is the conjugation of "prendre", asked during the LingQ import of last Tuesday. The
/// code that knows opens a scope, and the recorder reads it off whichever call happens inside.
///
/// Ambient rather than passed, because the calls happen several layers below anything that
/// knows the context - an import sends a query that asks a parser that asks the client - and
/// threading an argument through every one of those signatures for the sake of a log would
/// cost more than it explains. It flows with the async call, so concurrent requests never see
/// each other's scope.
///
/// Scopes nest, and an inner one keeps whatever it does not say from the outer: the import
/// sets the import, the parser inside it sets the word.
/// </remarks>
public static class ExternalCallScope
{
    private static readonly AsyncLocal<Frame?> CurrentFrame = new();

    public static Frame? Current => CurrentFrame.Value;

    /// <summary>Opens a scope, until the returned handle is disposed.</summary>
    /// <remarks>Dispose it in the same method that opened it - a <c>using</c> is the only safe way.</remarks>
    public static IDisposable Begin(
        ExternalCallPurpose? purpose = null,
        string? target = null,
        int? wordId = null,
        int? importId = null,
        string? promptVersion = null)
    {
        var outer = CurrentFrame.Value;

        CurrentFrame.Value = new Frame(
            purpose ?? outer?.Purpose,
            target ?? outer?.Target,
            wordId ?? outer?.WordId,
            importId ?? outer?.ImportId,
            promptVersion ?? outer?.PromptVersion);

        return new Restore(outer);
    }

    public sealed record Frame(
        ExternalCallPurpose? Purpose, string? Target, int? WordId, int? ImportId, string? PromptVersion = null);

    private sealed class Restore : IDisposable
    {
        private readonly Frame? _outer;

        public Restore(Frame? outer)
        {
            _outer = outer;
        }

        public void Dispose()
        {
            CurrentFrame.Value = _outer;
        }
    }
}
