using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddedFrequencyWord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "EncounterCount",
                table: "Words",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "EncounterCount",
                table: "Words",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
