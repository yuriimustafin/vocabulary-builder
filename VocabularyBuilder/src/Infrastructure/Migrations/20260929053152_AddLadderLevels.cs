using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLadderLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastExerciseType",
                table: "ReviewCards",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhaseRetrievals",
                table: "ReviewCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RungStreak",
                table: "ReviewCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // The ladder went from six single-exercise rungs (reveal, word-to-meaning choice,
            // meaning-to-word choice, cloze, scramble, recall) to four levels (introduction,
            // recognition, scaffolded, production). Each card keeps the level its old rung
            // belonged to; a word that had got past multiple choice starts scaffolded, since
            // nothing it had been asked yet was production.
            migrationBuilder.Sql(
                """
                UPDATE ReviewCards SET CurrentRung = CASE
                    WHEN CurrentRung >= 5 THEN 3
                    WHEN CurrentRung >= 3 THEN 2
                    WHEN CurrentRung >= 1 THEN 1
                    ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back onto the old rungs, each level onto the first rung it covered.
            migrationBuilder.Sql(
                """
                UPDATE ReviewCards SET CurrentRung = CASE
                    WHEN CurrentRung >= 3 THEN 5
                    WHEN CurrentRung = 2 THEN 3
                    ELSE CurrentRung
                END;
                """);

            migrationBuilder.DropColumn(
                name: "LastExerciseType",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "PhaseRetrievals",
                table: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "RungStreak",
                table: "ReviewCards");
        }
    }
}
