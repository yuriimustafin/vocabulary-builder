using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExampleGlossesDropCollocates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollocateTranslations",
                table: "WordStudyContents");

            migrationBuilder.DropColumn(
                name: "Collocates",
                table: "WordStudyContents");

            migrationBuilder.DropColumn(
                name: "NonCollocateTranslations",
                table: "WordStudyContents");

            migrationBuilder.DropColumn(
                name: "NonCollocates",
                table: "WordStudyContents");

            migrationBuilder.AddColumn<string>(
                name: "GlossTranslations",
                table: "StudyExamples",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlossWords",
                table: "StudyExamples",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlossTranslations",
                table: "StudyExamples");

            migrationBuilder.DropColumn(
                name: "GlossWords",
                table: "StudyExamples");

            migrationBuilder.AddColumn<string>(
                name: "CollocateTranslations",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Collocates",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NonCollocateTranslations",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NonCollocates",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);
        }
    }
}
