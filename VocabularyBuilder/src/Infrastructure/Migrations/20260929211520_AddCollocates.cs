using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollocates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Collocates",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NonCollocates",
                table: "WordStudyContents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Collocates",
                table: "WordStudyContents");

            migrationBuilder.DropColumn(
                name: "NonCollocates",
                table: "WordStudyContents");
        }
    }
}
