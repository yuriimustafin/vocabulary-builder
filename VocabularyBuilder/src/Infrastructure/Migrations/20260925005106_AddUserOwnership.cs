using Microsoft.EntityFrameworkCore.Migrations;
using VocabularyBuilder.Infrastructure.Identity;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Words_Headword_Language",
                table: "Words");

            migrationBuilder.DropIndex(
                name: "IX_ImportedBookWords_Headword_Language",
                table: "ImportedBookWords");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Words",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "VocabularyLists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "TodoLists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "ImportedBookWords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Everything collected so far needs a real owner before the foreign keys go on.
            // The placeholder user is made for it - it has no password, and the administrator
            // takes it over on startup, see LegacyOwner - but only when there is something to
            // own, so a new database starts with no users at all
            migrationBuilder.Sql($"""
                INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "EmailConfirmed",
                    "SecurityStamp", "ConcurrencyStamp", "PhoneNumberConfirmed", "TwoFactorEnabled",
                    "LockoutEnabled", "AccessFailedCount")
                SELECT '{LegacyOwner.Id}', '{LegacyOwner.UserName}', '{LegacyOwner.UserName.ToUpperInvariant()}', 0,
                    upper(hex(randomblob(16))), lower(hex(randomblob(16))), 0, 0,
                    1, 0
                WHERE EXISTS (SELECT 1 FROM "Words")
                   OR EXISTS (SELECT 1 FROM "VocabularyLists")
                   OR EXISTS (SELECT 1 FROM "TodoLists")
                   OR EXISTS (SELECT 1 FROM "ImportedBookWords");

                UPDATE "Words" SET "OwnerId" = '{LegacyOwner.Id}';
                UPDATE "VocabularyLists" SET "OwnerId" = '{LegacyOwner.Id}';
                UPDATE "TodoLists" SET "OwnerId" = '{LegacyOwner.Id}';
                UPDATE "ImportedBookWords" SET "OwnerId" = '{LegacyOwner.Id}';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Words_OwnerId_Headword_Language",
                table: "Words",
                columns: new[] { "OwnerId", "Headword", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyLists_OwnerId",
                table: "VocabularyLists",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TodoLists_OwnerId",
                table: "TodoLists",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportedBookWords_OwnerId_Headword_Language",
                table: "ImportedBookWords",
                columns: new[] { "OwnerId", "Headword", "Language" });

            migrationBuilder.AddForeignKey(
                name: "FK_ImportedBookWords_AspNetUsers_OwnerId",
                table: "ImportedBookWords",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TodoLists_AspNetUsers_OwnerId",
                table: "TodoLists",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VocabularyLists_AspNetUsers_OwnerId",
                table: "VocabularyLists",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Words_AspNetUsers_OwnerId",
                table: "Words",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Going back to one shared collection only works while it still is one: with a
            // second user's words in it, two copies of "maison" would break the old unique
            // index. The placeholder, or the administrator it became, is left as it is.
            migrationBuilder.DropForeignKey(
                name: "FK_ImportedBookWords_AspNetUsers_OwnerId",
                table: "ImportedBookWords");

            migrationBuilder.DropForeignKey(
                name: "FK_TodoLists_AspNetUsers_OwnerId",
                table: "TodoLists");

            migrationBuilder.DropForeignKey(
                name: "FK_VocabularyLists_AspNetUsers_OwnerId",
                table: "VocabularyLists");

            migrationBuilder.DropForeignKey(
                name: "FK_Words_AspNetUsers_OwnerId",
                table: "Words");

            migrationBuilder.DropIndex(
                name: "IX_Words_OwnerId_Headword_Language",
                table: "Words");

            migrationBuilder.DropIndex(
                name: "IX_VocabularyLists_OwnerId",
                table: "VocabularyLists");

            migrationBuilder.DropIndex(
                name: "IX_TodoLists_OwnerId",
                table: "TodoLists");

            migrationBuilder.DropIndex(
                name: "IX_ImportedBookWords_OwnerId_Headword_Language",
                table: "ImportedBookWords");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Words");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "VocabularyLists");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "TodoLists");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ImportedBookWords");

            migrationBuilder.CreateIndex(
                name: "IX_Words_Headword_Language",
                table: "Words",
                columns: new[] { "Headword", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedBookWords_Headword_Language",
                table: "ImportedBookWords",
                columns: new[] { "Headword", "Language" });
        }
    }
}
