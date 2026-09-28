using VocabularyBuilder.Domain.Samples.ValueObjects;

namespace VocabularyBuilder.Domain.Samples.Entities;

public class TodoList : BaseAuditableEntity, IOwnedEntity
{
    public string OwnerId { get; set; } = string.Empty;

    public string? Title { get; set; }

    public Colour Colour { get; set; } = Colour.White;

    public IList<TodoItem> Items { get; private set; } = new List<TodoItem>();
}
