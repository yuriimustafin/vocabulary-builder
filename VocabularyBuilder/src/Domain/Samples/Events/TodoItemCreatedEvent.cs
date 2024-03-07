using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Domain.Samples.Events;

public class TodoItemCreatedEvent : BaseEvent
{
    public TodoItemCreatedEvent(TodoItem item)
    {
        Item = item;
    }

    public TodoItem Item { get; }
}
