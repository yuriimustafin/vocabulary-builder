using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RelationsPoSAndEncounterAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EncounterCount",
                table: "Words",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartOfSpeech",
                table: "Sense",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WordId",
                table: "ImportedBookWords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedBookWords_WordId",
                table: "ImportedBookWords",
                column: "WordId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportedBookWords_Words_WordId",
                table: "ImportedBookWords",
                column: "WordId",
                principalTable: "Words",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportedBookWords_Words_WordId",
                table: "ImportedBookWords");

            migrationBuilder.DropIndex(
                name: "IX_ImportedBookWords_WordId",
                table: "ImportedBookWords");

            migrationBuilder.DropColumn(
                name: "EncounterCount",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "PartOfSpeech",
                table: "Sense");

            migrationBuilder.DropColumn(
                name: "WordId",
                table: "ImportedBookWords");
        }
    }
}
