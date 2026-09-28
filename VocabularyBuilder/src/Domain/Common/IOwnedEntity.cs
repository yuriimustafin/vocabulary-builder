namespace VocabularyBuilder.Domain.Common;

/// <summary>
/// An entity that belongs to one user, and that everything hanging off it belongs to as well.
/// </summary>
/// <remarks>
/// Only the roots carry the owner - a word, a list, an imported book entry. A sense, an
/// encounter or a review card belongs to whoever owns its word, and is scoped through it.
///
/// Nothing in the application sets this. The database context fills it in from the current
/// user when the entity is first saved, and refuses to save one when there is no user, so a
/// handler cannot forget it and cannot be talked into writing someone else's id.
/// </remarks>
public interface IOwnedEntity
{
    /// <summary>
    /// Id of the user the entity belongs to.
    /// </summary>
    string OwnerId { get; set; }
}
