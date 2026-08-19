using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusAssistant.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FocusTimeMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DistractionEvents = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductivityScore = table.Column<double>(type: "REAL", nullable: false),
                    MostUsedAppsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "WorkSessions",
                columns: table => new
                {
                    wID = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ProductiveTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    DistractedTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    BreakTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ProductivityScore = table.Column<double>(type: "REAL", nullable: false),
                    AppSwitches = table.Column<int>(type: "INTEGER", nullable: false),
                    TopAppsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSessions", x => x.wID);
                    table.ForeignKey(
                        name: "FK_WorkSessions_UserSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "UserSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUsages",
                columns: table => new
                {
                    aID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    wID = table.Column<string>(type: "TEXT", nullable: false),
                    AppName = table.Column<string>(type: "TEXT", nullable: false),
                    WindowTitle = table.Column<string>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    IsProductive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsages", x => x.aID);
                    table.ForeignKey(
                        name: "FK_AppUsages_WorkSessions_wID",
                        column: x => x.wID,
                        principalTable: "WorkSessions",
                        principalColumn: "wID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsages_StartTime",
                table: "AppUsages",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsages_wID",
                table: "AppUsages",
                column: "wID");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_StartTime",
                table: "UserSessions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_SessionId",
                table: "WorkSessions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSessions_StartTime",
                table: "WorkSessions",
                column: "StartTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppUsages");

            migrationBuilder.DropTable(
                name: "WorkSessions");

            migrationBuilder.DropTable(
                name: "UserSessions");
        }
    }
}
