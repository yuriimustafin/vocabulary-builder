namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// Settings for the model-backed dictionary, bound from the "OpenAI" section.
/// </summary>
/// <remarks>
/// The other keys in that section - <c>ApiKey</c> and <c>UseMockMode</c> - are deliberately
/// **not** on this class. They are read with <c>configuration.GetValue</c> while the services
/// are being registered, and binding them here as well would produce exactly the trap
/// <c>WordReferenceOptions.UseMockMode</c> is: a property that takes the value you set and is
/// read by nobody, while the implementation chosen at startup carries on regardless.
/// </remarks>
public class GptDictionaryOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>
    /// Ask the model for a verb's conjugation table.
    /// </summary>
    /// <remarks>
    /// A second call per verb, so it is worth being able to turn off - but on by default,
    /// because WordReference, which is where conjugations used to come from, is unreachable
    /// from the deployed host. Only verbs cost anything: nothing else is asked.
    /// </remarks>
    public bool IncludeConjugations { get; set; } = true;
}
