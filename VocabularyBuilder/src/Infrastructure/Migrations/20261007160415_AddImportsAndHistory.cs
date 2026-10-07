using System;
using Microsoft.EntityFrameworkCore.Migrations;
using VocabularyBuilder.Infrastructure.Data;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportsAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Language = table.Column<int>(type: "INTEGER", nullable: true),
                    WordId = table.Column<int>(type: "INTEGER", nullable: true),
                    Headword = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ListId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportId = table.Column<int>(type: "INTEGER", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLog_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalCallLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Target = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Request = table.Column<string>(type: "TEXT", nullable: true),
                    Response = table.Column<string>(type: "TEXT", nullable: true),
                    ResponseLength = table.Column<int>(type: "INTEGER", nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    IsMock = table.Column<bool>(type: "INTEGER", nullable: false),
                    WordId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalCallLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalCallLog_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    SourceIdentifierBase = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsReconstructed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TermsRead = table.Column<int>(type: "INTEGER", nullable: false),
                    WordsCreated = table.Column<int>(type: "INTEGER", nullable: false),
                    WordsTouched = table.Column<int>(type: "INTEGER", nullable: false),
                    EncountersCreated = table.Column<int>(type: "INTEGER", nullable: false),
                    TermsSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyImports_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyImportItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportId = table.Column<int>(type: "INTEGER", nullable: false),
                    WordId = table.Column<int>(type: "INTEGER", nullable: true),
                    Headword = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceTerm = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    EncounterAdded = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyImportItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyImportItems_VocabularyImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "VocabularyImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VocabularyImportItems_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_ImportId",
                table: "ActivityLog",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_OwnerId_Id",
                table: "ActivityLog",
                columns: new[] { "OwnerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_WordId",
                table: "ActivityLog",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCallLog_ImportId",
                table: "ExternalCallLog",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCallLog_OwnerId_Id",
                table: "ExternalCallLog",
                columns: new[] { "OwnerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCallLog_WordId",
                table: "ExternalCallLog",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyImportItems_ImportId",
                table: "VocabularyImportItems",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyImportItems_WordId",
                table: "VocabularyImportItems",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyImports_OwnerId_StartedAtUtc",
                table: "VocabularyImports",
                columns: new[] { "OwnerId", "StartedAtUtc" });

            // Imports made before there was anything to record them: put back together from
            // their encounters, and marked as reconstructed
            migrationBuilder.Sql(ImportBackfill.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLog");

            migrationBuilder.DropTable(
                name: "ExternalCallLog");

            migrationBuilder.DropTable(
                name: "VocabularyImportItems");

            migrationBuilder.DropTable(
                name: "VocabularyImports");
        }
    }
}
