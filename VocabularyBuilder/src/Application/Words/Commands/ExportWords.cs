using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Commands;

public record ExportWordsCommand(
    List<int> WordIds) : IRequest<ExportWordsResult>;

public record ExportWordsResult(
    string CsvContent,
    int ExportedCount);

public class ExportWordsCommandHandler : IRequestHandler<ExportWordsCommand, ExportWordsResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IWordsExporter _wordsExporter;

    public ExportWordsCommandHandler(
        IApplicationDbContext context,
        IWordsExporter wordsExporter)
    {
        _context = context;
        _wordsExporter = wordsExporter;
    }

    public async Task<ExportWordsResult> Handle(ExportWordsCommand request, CancellationToken cancellationToken)
    {
        // Fetch words with all necessary data
        var words = await _context.Words
            .Include(w => w.Senses!)
            .Where(w => request.WordIds.Contains(w.Id))
            .ToListAsync(cancellationToken);

        if (!words.Any())
        {
            return new ExportWordsResult(string.Empty, 0);
        }

        // Update status and generate SyncId before exporting (so it's included in CSV)
        foreach (var word in words)
        {
            word.Status = WordStatus.Exported;
            // Generate SyncId only if it doesn't exist yet
            if (!word.SyncId.HasValue)
            {
                word.SyncId = Guid.NewGuid();
            }
        }

        // Generate CSV content (includes SyncId)
        var csvContent = _wordsExporter.ExportWords(words);

        // Save changes to database
        await _context.SaveChangesAsync(cancellationToken);

        return new ExportWordsResult(csvContent, words.Count);
    }
}
