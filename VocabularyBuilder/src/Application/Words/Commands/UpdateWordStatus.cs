using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Commands;

public record UpdateWordStatusCommand : IRequest
{
    public int Id { get; init; }
    public WordStatus Status { get; init; }
}

public class UpdateWordStatusCommandHandler : IRequestHandler<UpdateWordStatusCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateWordStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateWordStatusCommand request, CancellationToken cancellationToken)
    {
        var word = await _context.Words
            .FindAsync(new object[] { request.Id }, cancellationToken);

        Guard.Against.NotFound(request.Id, word);

        word.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
