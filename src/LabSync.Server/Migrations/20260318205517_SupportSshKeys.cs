using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabSync.Server.Migrations
{
    /// <inheritdoc />
    public partial class SupportSshKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SshPassword",
                table: "DeviceCredentials",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<bool>(
                name: "UseKeyAuthentication",
                table: "DeviceCredentials",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseKeyAuthentication",
                table: "DeviceCredentials");

            migrationBuilder.AlterColumn<string>(
                name: "SshPassword",
                table: "DeviceCredentials",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
