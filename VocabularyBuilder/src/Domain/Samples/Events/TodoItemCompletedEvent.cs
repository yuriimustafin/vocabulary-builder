using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Domain.Samples.Events;

public class TodoItemCompletedEvent : BaseEvent
{
    public TodoItemCompletedEvent(TodoItem item)
    {
        Item = item;
    }

    public TodoItem Item { get; }
}
