using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Commands;

/// <summary>
/// Takes a word out of study because the learner already knows it.
/// </summary>
public record MarkWordAsKnownCommand(int CardId) : IRequest<bool>;

/// <summary>
/// Recognising a word on sight is worth acting on: studying it would spend a place in the
/// day on something already learned.
///
/// The card is removed rather than suspended, so the day's allowance is handed back and
/// another word takes its place immediately. The word itself is marked Known, which is what
/// stops it being offered again - without that it would simply be picked afresh tomorrow,
/// having no card to show it had ever been seen.
///
/// This is the one place the study loop writes a word's status, and it does so because the
/// learner said to, not because the scheduler inferred anything.
/// </summary>
public class MarkWordAsKnownCommandHandler : IRequestHandler<MarkWordAsKnownCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public MarkWordAsKnownCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(MarkWordAsKnownCommand request, CancellationToken cancellationToken)
    {
        var card = await _context.ReviewCards
            .Include(c => c.Word)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken);

        if (card is null)
        {
            return false;
        }

        card.Word.Status = WordStatus.Known;
        card.Word.IsMarkedForStudy = false;

        var logs = await _context.ReviewLogs
            .Where(l => l.ReviewCardId == card.Id)
            .ToListAsync(cancellationToken);

        _context.ReviewLogs.RemoveRange(logs);
        _context.ReviewCards.Remove(card);

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
