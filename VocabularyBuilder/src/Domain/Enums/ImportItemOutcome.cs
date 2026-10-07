namespace VocabularyBuilder.Domain.Enums;

/// <summary>What an import did with one of the terms it was given.</summary>
public enum ImportItemOutcome
{
    /// <summary>The term brought in a word the vocabulary did not have.</summary>
    Created = 0,

    /// <summary>The term landed on a word that was already there.</summary>
    Existing = 1,

    /// <summary>The term was set aside - a sentence, a question, an expression.</summary>
    Skipped = 2
}
