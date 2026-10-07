using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyExamplesAndConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cognates",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Etymology",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mnemonic",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Usage",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Form",
                table: "WordEncounters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudyExamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sentence = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Translation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Form = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Collocation = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Successes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyExamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyExamples_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyExamples_WordId",
                table: "StudyExamples",
                column: "WordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyExamples");

            migrationBuilder.DropColumn(
                name: "Cognates",
                table: "WordStudyContents");

            migrationBuilder.DropColumn(
                name: "Etymology",
                table: "WordStudyContents");

            migrationBuilder.DropColumn(
                name: "Mnemonic",
                table: "WordStudyContents");

            migrationBuilder.DropColumn(
                name: "Usage",
                table: "WordStudyContents");

            migrationBuilder.DropColumn(
                name: "Form",
                table: "WordEncounters");
        }
    }
}
