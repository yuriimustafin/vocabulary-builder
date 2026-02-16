using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFrequencyWordStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FrequencyWords_FrequencyWords_LemmaId",
                table: "FrequencyWords");

            migrationBuilder.DropIndex(
                name: "IX_FrequencyWords_LemmaId",
                table: "FrequencyWords");

            migrationBuilder.DropColumn(
                name: "LemmaId",
                table: "FrequencyWords");

            migrationBuilder.AlterColumn<int>(
                name: "Frequency",
                table: "FrequencyWords",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "BaseFormId",
                table: "FrequencyWords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FrequencyWords_BaseFormId",
                table: "FrequencyWords",
                column: "BaseFormId");

            migrationBuilder.AddForeignKey(
                name: "FK_FrequencyWords_FrequencyWords_BaseFormId",
                table: "FrequencyWords",
                column: "BaseFormId",
                principalTable: "FrequencyWords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FrequencyWords_FrequencyWords_BaseFormId",
                table: "FrequencyWords");

            migrationBuilder.DropIndex(
                name: "IX_FrequencyWords_BaseFormId",
                table: "FrequencyWords");

            migrationBuilder.DropColumn(
                name: "BaseFormId",
                table: "FrequencyWords");

            migrationBuilder.AlterColumn<int>(
                name: "Frequency",
                table: "FrequencyWords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LemmaId",
                table: "FrequencyWords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FrequencyWords_LemmaId",
                table: "FrequencyWords",
                column: "LemmaId");

            migrationBuilder.AddForeignKey(
                name: "FK_FrequencyWords_FrequencyWords_LemmaId",
                table: "FrequencyWords",
                column: "LemmaId",
                principalTable: "FrequencyWords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
