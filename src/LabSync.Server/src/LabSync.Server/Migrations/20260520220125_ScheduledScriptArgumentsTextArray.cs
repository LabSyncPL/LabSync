using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabSync.Server.src.LabSync.Server.Migrations
{
    /// <inheritdoc />
    public partial class ScheduledScriptArgumentsTextArray : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string[]>(
                name: "Arguments",
                table: "ScheduledScripts",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Arguments",
                table: "ScheduledScripts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string[]),
                oldType: "text[]");
        }
    }
}
