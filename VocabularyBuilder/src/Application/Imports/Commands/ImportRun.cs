using VocabularyBuilder.Application.History;

namespace VocabularyBuilder.Application.Imports.Commands;

/// <summary>
/// The frame every import page's handler runs its work in.
/// </summary>
/// <remarks>
/// Opens the import's record, makes it the scope of every model and dictionary call made
/// inside, and marks it failed if the work throws. The work is handed the import's id to file
/// its words under, and is expected to finish with <see cref="CompleteImportCommand"/> itself,
/// since only it knows how many terms it read and which it skipped.
/// </remarks>
public static class ImportRun
{
    public static async Task<T> TrackAsync<T>(
        ISender sender,
        StartImportCommand start,
        Func<int, Task<T>> work,
        CancellationToken cancellationToken)
    {
        var importId = await sender.Send(start, cancellationToken);

        using var scope = ExternalCallScope.Begin(importId: importId > 0 ? importId : null);

        try
        {
            return await work(importId);
        }
        catch (Exception ex)
        {
            try
            {
                // Not the request's token: a cancelled import is exactly one worth marking
                await sender.Send(new FailImportCommand(importId, ex.Message), CancellationToken.None);
            }
            catch
            {
                // The import's own failure is the one to report
            }

            throw;
        }
    }
}
