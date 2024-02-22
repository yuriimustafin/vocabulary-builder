using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportedBookWordsUpdated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Page",
                table: "ImportedBookWords");

            migrationBuilder.AddColumn<int>(
                name: "Frequency",
                table: "Words",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Heading",
                table: "ImportedBookWords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ImportedBookWords",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "Heading",
                table: "ImportedBookWords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ImportedBookWords");

            migrationBuilder.AddColumn<int>(
                name: "Page",
                table: "ImportedBookWords",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
