namespace VocabularyBuilder.Domain.Enums;

public enum ImportStatus
{
    /// <summary>Started and not yet finished - or stopped by something that left no chance to say so.</summary>
    Running = 0,

    Completed = 1,

    Failed = 2
}
