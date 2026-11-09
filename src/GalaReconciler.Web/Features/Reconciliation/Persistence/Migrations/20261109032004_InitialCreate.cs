using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GalaReconciler.Web.Features.Reconciliation.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    SourceFileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    ProcessedFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ProcessedFileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<double>(type: "REAL", nullable: true),
                    SourceGuestCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedGuestCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GuestNotFoundCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DatesMismatchCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MobileNumberMismatchCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EmailMismatchCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    MissingHeaders = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Discrepancies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    GuestDisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    HostDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    SourceDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    MatchedBy = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Detail = table.Column<string>(type: "TEXT", nullable: false),
                    PossibleMatch = table.Column<string>(type: "TEXT", nullable: true),
                    SourceValue = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessedValue = table.Column<string>(type: "TEXT", nullable: true),
                    DaysJson = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRowsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedRowsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discrepancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Discrepancies_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Warnings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    File = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warnings_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_GuestDisplayName",
                table: "Discrepancies",
                column: "GuestDisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_RunId_Position",
                table: "Discrepancies",
                columns: new[] { "RunId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_CreatedAtUtc",
                table: "Runs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_Status",
                table: "Runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_RunId_Position",
                table: "Warnings",
                columns: new[] { "RunId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Discrepancies");

            migrationBuilder.DropTable(
                name: "Warnings");

            migrationBuilder.DropTable(
                name: "Runs");
        }
    }
}
