using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocabularyBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMarkedForStudy",
                table: "Words",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ReviewCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WordId = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentRung = table.Column<int>(type: "INTEGER", nullable: false),
                    EaseFactor = table.Column<double>(type: "REAL", nullable: false),
                    IntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    LearningStepIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Lapses = table.Column<int>(type: "INTEGER", nullable: false),
                    LapsesSinceRecovery = table.Column<int>(type: "INTEGER", nullable: false),
                    RecentSuccessRate = table.Column<double>(type: "REAL", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IntroducedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewCards_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WordStudyContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedDefinition = table.Column<string>(type: "TEXT", nullable: true),
                    GeneratedContextSentence = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GenerationAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    PromptVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordStudyContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WordStudyContents_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReviewCardId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExerciseType = table.Column<int>(type: "INTEGER", nullable: false),
                    Grade = table.Column<int>(type: "INTEGER", nullable: false),
                    GradeWasSelfReported = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsScaffold = table.Column<bool>(type: "INTEGER", nullable: false),
                    ElapsedMs = table.Column<int>(type: "INTEGER", nullable: false),
                    HintUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    StateBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    RungBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalBeforeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalAfterDays = table.Column<int>(type: "INTEGER", nullable: false),
                    EaseFactorAfter = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewLogs_ReviewCards_ReviewCardId",
                        column: x => x.ReviewCardId,
                        principalTable: "ReviewCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_IntroducedAtUtc",
                table: "ReviewCards",
                column: "IntroducedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_State_DueAtUtc",
                table: "ReviewCards",
                columns: new[] { "State", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCards_WordId",
                table: "ReviewCards",
                column: "WordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLogs_AttemptId",
                table: "ReviewLogs",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLogs_ReviewCardId_ReviewedAtUtc",
                table: "ReviewLogs",
                columns: new[] { "ReviewCardId", "ReviewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WordStudyContents_Status_ClaimedAtUtc",
                table: "WordStudyContents",
                columns: new[] { "Status", "ClaimedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WordStudyContents_WordId",
                table: "WordStudyContents",
                column: "WordId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewLogs");

            migrationBuilder.DropTable(
                name: "WordStudyContents");

            migrationBuilder.DropTable(
                name: "ReviewCards");

            migrationBuilder.DropColumn(
                name: "IsMarkedForStudy",
                table: "Words");
        }
    }
}
