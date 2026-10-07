namespace VocabularyBuilder.Infrastructure.Data;

/// <summary>
/// Rebuilds imports from the encounters written before imports were recorded.
/// </summary>
/// <remarks>
/// Run once, by the <c>AddImportsAndHistory</c> migration, and kept here so a test can run
/// exactly the same statement against data of its own. It is part of that migration's
/// history: change it and a database migrated later reconstructs differently from one
/// migrated earlier. Fix a mistake in a new migration instead.
///
/// An encounter from an import has an identifier of the form <c>{base}:{term}</c>, and the
/// base is what the import was run under - a list name, a book title, a hash of the pasted
/// text. The imports are those bases, one per day they were used on, which splits a LingQ
/// export imported again a week later into two imports - which is what it was.
///
/// The base cannot simply be read up to the first colon: book titles have colons of their own
/// ("Mistborn: The Final Empire:ruddy:1"). Where the encounter's context is a prefix of its
/// identifier - a book title, a named list - that is the base; otherwise it is up to the
/// first colon, which holds for the hashes and for LingQ's "lingq".
///
/// Manual and API encounters are left out: they were never imports.
///
/// A term counts as creating its word when the word's first encounter of all belongs to the
/// same reconstructed import. Nothing about skipped terms survived, so there are none.
/// </remarks>
public static class ImportBackfill
{
    public const string Sql = """
        DROP TABLE IF EXISTS temp.ImportBackfillEncounters;

        CREATE TEMP TABLE ImportBackfillEncounters AS
        SELECT
            e.Id AS EncounterId,
            e.WordId,
            w.OwnerId,
            w.Language,
            w.Headword,
            e.Source,
            e.Context,
            e.SourceIdentifier,
            e.Created,
            e.Form,
            CASE
                WHEN e.Context IS NOT NULL AND e.Context <> ''
                     AND substr(e.SourceIdentifier, 1, length(e.Context) + 1) = e.Context || ':'
                THEN e.Context
                ELSE substr(e.SourceIdentifier, 1, instr(e.SourceIdentifier, ':') - 1)
            END AS Prefix,
            substr(e.Created, 1, 10) AS Day
        FROM WordEncounters e
        JOIN Words w ON w.Id = e.WordId
        WHERE e.Source IN (1, 2, 3, 5, 6)
          AND instr(e.SourceIdentifier, ':') > 1;

        DROP TABLE IF EXISTS temp.ImportBackfillGroups;

        CREATE TEMP TABLE ImportBackfillGroups AS
        SELECT
            OwnerId,
            Language,
            Source,
            Prefix,
            Day,
            CASE Source WHEN 1 THEN 1 WHEN 5 THEN 2 WHEN 6 THEN 3 ELSE 0 END AS Kind,
            MAX(CASE WHEN Prefix = Context THEN Context END) AS Name,
            MIN(Created) AS FirstCreated,
            MAX(Created) AS LastCreated,
            COUNT(*) AS Encounters,
            COUNT(DISTINCT WordId) AS Words
        FROM ImportBackfillEncounters
        GROUP BY OwnerId, Language, Source, Prefix, Day;

        -- A DateTimeOffset column is stored with its offset and a DateTime column without,
        -- and every timestamp the app writes is UTC
        ALTER TABLE ImportBackfillGroups ADD COLUMN StartedAtUtc TEXT;
        ALTER TABLE ImportBackfillGroups ADD COLUMN CompletedAtUtc TEXT;

        UPDATE ImportBackfillGroups SET
            StartedAtUtc = CASE WHEN FirstCreated LIKE '%+00:00'
                THEN substr(FirstCreated, 1, length(FirstCreated) - 6) ELSE datetime(FirstCreated) END,
            CompletedAtUtc = CASE WHEN LastCreated LIKE '%+00:00'
                THEN substr(LastCreated, 1, length(LastCreated) - 6) ELSE datetime(LastCreated) END;

        INSERT INTO VocabularyImports (
            OwnerId, Kind, Language, Name, FileName, SourceIdentifierBase, Tags, Status,
            StartedAtUtc, CompletedAtUtc, Error, IsReconstructed,
            TermsRead, WordsCreated, WordsTouched, EncountersCreated, TermsSkipped,
            Created, CreatedBy, LastModified, LastModifiedBy)
        SELECT
            OwnerId, Kind, Language, Name, NULL, Prefix, NULL, 1,
            StartedAtUtc, CompletedAtUtc, NULL, 1,
            Encounters, 0, Words, Encounters, 0,
            FirstCreated, NULL, LastCreated, NULL
        FROM ImportBackfillGroups
        ORDER BY StartedAtUtc;

        INSERT INTO VocabularyImportItems (
            ImportId, WordId, Headword, SourceTerm, Outcome, EncounterAdded, Reason, Form, ExampleAdded, ContentReopened)
        SELECT
            i.Id,
            b.WordId,
            b.Headword,
            -- A Kindle identifier ends in the page number, which is not part of the term
            CASE WHEN b.Source = 1 THEN b.Headword ELSE substr(b.SourceIdentifier, length(b.Prefix) + 2) END,
            CASE WHEN EXISTS (
                SELECT 1
                FROM ImportBackfillEncounters f
                WHERE f.EncounterId = (SELECT MIN(e2.Id) FROM WordEncounters e2 WHERE e2.WordId = b.WordId)
                  AND f.OwnerId = b.OwnerId AND f.Language = b.Language AND f.Source = b.Source
                  AND f.Prefix = b.Prefix AND f.Day = b.Day)
            THEN 0 ELSE 1 END,
            1,
            NULL,
            b.Form,
            0,
            0
        FROM ImportBackfillEncounters b
        JOIN ImportBackfillGroups g
            ON g.OwnerId = b.OwnerId AND g.Language = b.Language AND g.Source = b.Source
           AND g.Prefix = b.Prefix AND g.Day = b.Day
        JOIN VocabularyImports i
            ON i.IsReconstructed = 1 AND i.OwnerId = g.OwnerId AND i.Language = g.Language
           AND i.Kind = g.Kind AND i.SourceIdentifierBase = g.Prefix AND i.StartedAtUtc = g.StartedAtUtc
        ORDER BY b.EncounterId;

        UPDATE VocabularyImports SET WordsCreated = (
            SELECT COUNT(DISTINCT it.WordId)
            FROM VocabularyImportItems it
            WHERE it.ImportId = VocabularyImports.Id AND it.Outcome = 0)
        WHERE IsReconstructed = 1;

        DROP TABLE temp.ImportBackfillGroups;
        DROP TABLE temp.ImportBackfillEncounters;
        """;
}
