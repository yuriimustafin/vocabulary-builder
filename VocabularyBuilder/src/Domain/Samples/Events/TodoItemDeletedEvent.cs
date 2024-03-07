using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Domain.Samples.Events;

public class TodoItemDeletedEvent : BaseEvent
{
    public TodoItemDeletedEvent(TodoItem item)
    {
        Item = item;
    }

    public TodoItem Item { get; }
}
