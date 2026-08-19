using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusAssistant.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkSessionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "WorkSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "WorkSessions");
        }
    }
}
