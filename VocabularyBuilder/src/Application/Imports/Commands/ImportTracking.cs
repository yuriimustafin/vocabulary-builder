using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Domain.Entities.Imports;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Imports.Commands;

/// <summary>
/// Opens the record of an import before it writes anything, so that every word it touches
/// can be filed under it.
/// </summary>
/// <remarks>
/// Saved immediately, and left <see cref="ImportStatus.Running"/> until the import says how it
/// ended. An import the process dies in the middle of therefore stays Running for ever, which
/// is the honest answer: it neither finished nor reported failing.
/// </remarks>
public record StartImportCommand : IRequest<int>
{
    public ImportKind Kind { get; init; }
    public Language Language { get; init; }
    public string? Name { get; init; }
    public string? FileName { get; init; }
    public string? SourceIdentifierBase { get; init; }
    public List<string>? Tags { get; init; }
}

public class StartImportCommandHandler : IRequestHandler<StartImportCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public StartImportCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<int> Handle(StartImportCommand request, CancellationToken cancellationToken)
    {
        var import = new VocabularyImport
        {
            Kind = request.Kind,
            Language = request.Language,
            Name = Blank(request.Name),
            FileName = Blank(request.FileName),
            SourceIdentifierBase = request.SourceIdentifierBase,
            Tags = request.Tags is { Count: > 0 } ? request.Tags : null,
            Status = ImportStatus.Running,
            StartedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        _context.VocabularyImports.Add(import);
        await _context.SaveChangesAsync(cancellationToken);

        return import.Id;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>What an import amounted to, counted from its items.</summary>
public record ImportSummary(int TermsRead, int WordsCreated, int WordsTouched, int EncountersCreated, int TermsSkipped);

/// <summary>
/// Closes an import: files the terms it set aside, counts what it did, and logs it.
/// </summary>
/// <remarks>
/// The counts are taken from the items rather than handed in, so that the list page, the
/// import's own popup and the result the import page shows can never disagree.
/// </remarks>
/// <param name="Name">A name the import only learnt while reading its source - a book title.</param>
public record CompleteImportCommand(
    int ImportId,
    int TermsRead,
    IReadOnlyList<SkippedTerm>? Skipped = null,
    string? Name = null)
    : IRequest<ImportSummary?>;

public class CompleteImportCommandHandler : IRequestHandler<CompleteImportCommand, ImportSummary?>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public CompleteImportCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<ImportSummary?> Handle(CompleteImportCommand request, CancellationToken cancellationToken)
    {
        var import = await _context.VocabularyImports
            .FirstOrDefaultAsync(i => i.Id == request.ImportId, cancellationToken);

        if (import is null)
        {
            return null;
        }

        foreach (var skipped in request.Skipped ?? Array.Empty<SkippedTerm>())
        {
            _context.VocabularyImportItems.Add(new VocabularyImportItem
            {
                ImportId = import.Id,
                SourceTerm = skipped.SourceTerm,
                Outcome = ImportItemOutcome.Skipped,
                Reason = skipped.Reason
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var items = await _context.VocabularyImportItems
            .Where(i => i.ImportId == import.Id)
            .Select(i => new { i.WordId, i.Outcome, i.EncounterAdded })
            .ToListAsync(cancellationToken);

        import.Name ??= string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        import.TermsRead = request.TermsRead;
        import.WordsCreated = items
            .Where(i => i.Outcome == ImportItemOutcome.Created)
            .Select(i => i.WordId)
            .Distinct()
            .Count();
        import.WordsTouched = items
            .Where(i => i.Outcome != ImportItemOutcome.Skipped)
            .Select(i => i.WordId)
            .Distinct()
            .Count();
        import.EncountersCreated = items.Count(i => i.EncounterAdded);
        import.TermsSkipped = items.Count(i => i.Outcome == ImportItemOutcome.Skipped);
        import.Status = ImportStatus.Completed;
        import.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        _context.RecordActivity(
            ActivityAction.ImportCompleted,
            language: import.Language,
            importId: import.Id,
            summary: $"{import.Kind}{(import.Name is null ? "" : $" \"{import.Name}\"")}: " +
                     $"{import.WordsCreated} new, {import.WordsTouched - import.WordsCreated} already known, " +
                     $"{import.TermsSkipped} skipped",
            details: new
            {
                import.Kind,
                import.Name,
                import.FileName,
                import.TermsRead,
                import.WordsCreated,
                import.WordsTouched,
                import.EncountersCreated,
                import.TermsSkipped
            });

        await _context.SaveChangesAsync(cancellationToken);

        return new ImportSummary(
            import.TermsRead, import.WordsCreated, import.WordsTouched, import.EncountersCreated, import.TermsSkipped);
    }
}

/// <summary>Marks an import as having failed, keeping whatever it managed before it did.</summary>
public record FailImportCommand(int ImportId, string Error) : IRequest;

public class FailImportCommandHandler : IRequestHandler<FailImportCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public FailImportCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task Handle(FailImportCommand request, CancellationToken cancellationToken)
    {
        // Whatever the import was saving when it failed is still tracked, and would fail again
        // on this save. It is lost either way; dropping it here is what lets the failure be
        // recorded at all
        _context.ChangeTracker.Clear();

        var import = await _context.VocabularyImports
            .FirstOrDefaultAsync(i => i.Id == request.ImportId, cancellationToken);

        if (import is null)
        {
            return;
        }

        import.Status = ImportStatus.Failed;
        import.Error = request.Error.Length > 2000 ? request.Error[..2000] : request.Error;
        import.CompletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        _context.RecordActivity(
            ActivityAction.ImportFailed,
            language: import.Language,
            importId: import.Id,
            summary: $"{import.Kind}{(import.Name is null ? "" : $" \"{import.Name}\"")}: {request.Error}");

        await _context.SaveChangesAsync(cancellationToken);
    }
}
