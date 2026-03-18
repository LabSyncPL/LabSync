using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabSync.Server.Migrations
{
    /// <inheritdoc />
    public partial class SecureSshKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SshPrivateKey",
                table: "DeviceCredentials");

            migrationBuilder.AddColumn<string>(
                name: "SshKeyReference",
                table: "DeviceCredentials",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SshKeyReference",
                table: "DeviceCredentials");

            migrationBuilder.AddColumn<string>(
                name: "SshPrivateKey",
                table: "DeviceCredentials",
                type: "text",
                nullable: true);
        }
    }
}
