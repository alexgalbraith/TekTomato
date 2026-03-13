#nullable disable
using Microsoft.EntityFrameworkCore.Migrations;

namespace TekTomato.Data.Migrations
{
    /// <summary>
    /// Initial migration creating Sessions and Settings tables.
    /// </summary>
    /// <remarks>
    /// Generated to match the Data Model specifications in Architecture Document v1.0, Section 3.
    /// </remarks>
    public partial class InitialCreate : Migration
    {
        /// <summary>
        /// Creates the tables and indexes on migration up.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Sessions Table
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlannedDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    OverrunSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    PausedDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CompletedNormally = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    PomodoroNumber = table.Column<int?>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            // Create Settings Table
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            // Create Indexes for Sessions
            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SessionType_CompletedNormally",
                table: "Sessions",
                columns: new[] { "SessionType", "CompletedNormally" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StartedAtUtc",
                table: "Sessions",
                column: "StartedAtUtc");
        }

        /// <summary>
        /// Drops the tables on migration down.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}