using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusAssistant.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInterventionsAndOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterventionOutcomes",
                columns: table => new
                {
                    iID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    wID = table.Column<string>(type: "TEXT", nullable: false),
                    ShownAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppName = table.Column<string>(type: "TEXT", nullable: false),
                    TriggerRationale = table.Column<string>(type: "TEXT", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    DistractionRisk = table.Column<double>(type: "REAL", nullable: false),
                    Response = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeToRespond = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    ReturnedToWork = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterventionOutcomes", x => x.iID);
                });

            migrationBuilder.CreateTable(
                name: "UserOverrides",
                columns: table => new
                {
                    oID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppName = table.Column<string>(type: "TEXT", nullable: false),
                    IsProductive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOverrides", x => x.oID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterventionOutcomes_ShownAt",
                table: "InterventionOutcomes",
                column: "ShownAt");

            migrationBuilder.CreateIndex(
                name: "IX_InterventionOutcomes_wID",
                table: "InterventionOutcomes",
                column: "wID");

            migrationBuilder.CreateIndex(
                name: "IX_UserOverrides_AppName",
                table: "UserOverrides",
                column: "AppName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterventionOutcomes");

            migrationBuilder.DropTable(
                name: "UserOverrides");
        }
    }
}
