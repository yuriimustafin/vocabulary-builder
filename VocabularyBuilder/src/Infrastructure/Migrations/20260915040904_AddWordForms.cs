using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWordForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WordForms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Form = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Language = table.Column<int>(type: "INTEGER", nullable: false),
                    Mood = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Tense = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Person = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WordForms_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WordForms_Form_Language",
                table: "WordForms",
                columns: new[] { "Form", "Language" });

            migrationBuilder.CreateIndex(
                name: "IX_WordForms_WordId_Form",
                table: "WordForms",
                columns: new[] { "WordId", "Form" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WordForms");
        }
    }
}
