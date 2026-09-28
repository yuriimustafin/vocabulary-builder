using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitSenseGlossAndExampleTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExampleTranslations",
                table: "Words",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExampleTranslations",
                table: "Sense",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gloss",
                table: "Sense",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExampleTranslations",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "ExampleTranslations",
                table: "Sense");

            migrationBuilder.DropColumn(
                name: "Gloss",
                table: "Sense");
        }
    }
}
