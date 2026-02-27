using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Language",
                table: "Words",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Language",
                table: "ImportedBookWords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Language",
                table: "FrequencyWords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Disable foreign keys temporarily to clean up duplicates
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF");

            // Handle duplicate FrequencyWords
            // Step 1: Create temp table with IDs to keep
            migrationBuilder.Sql(@"
                CREATE TEMPORARY TABLE FrequencyWordsToKeep AS
                SELECT MIN(Id) as KeepId, Headword, Language
                FROM FrequencyWords
                GROUP BY Headword, Language
            ");

            // Step 2: Update BaseFormId references to point to the ID we're keeping
            migrationBuilder.Sql(@"
                UPDATE FrequencyWords
                SET BaseFormId = (
                    SELECT tk.KeepId
                    FROM FrequencyWordsToKeep tk
                    INNER JOIN FrequencyWords base ON base.Id = FrequencyWords.BaseFormId
                    WHERE base.Headword = tk.Headword AND base.Language = tk.Language
                )
                WHERE BaseFormId IS NOT NULL
                AND BaseFormId NOT IN (SELECT KeepId FROM FrequencyWordsToKeep)
            ");

            // Step 3: Delete duplicates
            migrationBuilder.Sql(@"
                DELETE FROM FrequencyWords
                WHERE Id NOT IN (SELECT KeepId FROM FrequencyWordsToKeep)
            ");

            // Step 4: Drop temp table
            migrationBuilder.Sql("DROP TABLE FrequencyWordsToKeep");

            // Re-enable foreign keys
            migrationBuilder.Sql("PRAGMA foreign_keys = ON");

            migrationBuilder.CreateIndex(
                name: "IX_Words_Headword_Language",
                table: "Words",
                columns: new[] { "Headword", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedBookWords_Headword_Language",
                table: "ImportedBookWords",
                columns: new[] { "Headword", "Language" });

            migrationBuilder.CreateIndex(
                name: "IX_FrequencyWords_Headword_Language",
                table: "FrequencyWords",
                columns: new[] { "Headword", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Words_Headword_Language",
                table: "Words");

            migrationBuilder.DropIndex(
                name: "IX_ImportedBookWords_Headword_Language",
                table: "ImportedBookWords");

            migrationBuilder.DropIndex(
                name: "IX_FrequencyWords_Headword_Language",
                table: "FrequencyWords");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "ImportedBookWords");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "FrequencyWords");
        }
    }
}
