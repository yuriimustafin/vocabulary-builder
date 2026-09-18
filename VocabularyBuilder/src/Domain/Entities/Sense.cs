using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Samples.Entities;
public class Sense : BaseAuditableEntity
{
    /// <summary>
    /// What the word means, in the learner's language. Stored on its own, with nothing
    /// wrapped around it: a bilingual dictionary also says which sense is meant, and that
    /// belongs in <see cref="Gloss"/> rather than in front of this in brackets. Anything
    /// that compares one meaning against another - picking wrong answers for a multiple
    /// choice, most of all - wants this and nothing else.
    /// </summary>
    public required string Definition { get; set; }

    /// <summary>
    /// Which sense is meant, in the language being learned - WordReference's "(pour dire
    /// bonjour en soirée)" beside "good evening". Null when the source does not say.
    /// </summary>
    public string? Gloss { get; set; }

    public PartsOfSpeech PartOfSpeech { get; set; }

    /// <summary>Gender of this meaning, for a noun in a language that has one.</summary>
    public GrammaticalGender? Gender { get; set; }

    /// <summary>True when this meaning only exists in the plural.</summary>
    public bool IsPluralOnly { get; set; }
    /// <summary>Example sentences, in the language being learned.</summary>
    public required IList<string> Examples { get; set; }

    /// <summary>
    /// Translations of <see cref="Examples"/>, paired by position. Held apart rather than
    /// written into the example itself, so that a cloze exercise blanks a sentence and not
    /// a sentence with its own translation trailing after it.
    /// </summary>
    public IList<string>? ExampleTranslations { get; set; }
}
