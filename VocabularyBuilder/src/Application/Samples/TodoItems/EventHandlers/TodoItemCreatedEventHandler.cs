using VocabularyBuilder.Domain.Samples.Events;
using Microsoft.Extensions.Logging;

namespace VocabularyBuilder.Application.Samples.TodoItems.EventHandlers;

public class TodoItemCreatedEventHandler : INotificationHandler<TodoItemCreatedEvent>
{
    private readonly ILogger<TodoItemCreatedEventHandler> _logger;

    public TodoItemCreatedEventHandler(ILogger<TodoItemCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TodoItemCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("VocabularyBuilder Domain Event: {DomainEvent}", notification.GetType().Name);

        return Task.CompletedTask;
    }
}
