using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Lists.Commands;

public record UpdateListCommand : IRequest<bool>
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public ListStatus Status { get; init; }
}

public class UpdateListCommandHandler : IRequestHandler<UpdateListCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateListCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateListCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.VocabularyLists.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        entity.Title = request.Title;
        entity.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
