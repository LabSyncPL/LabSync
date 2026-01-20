using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabSync.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Devices");
        }
    }
}
