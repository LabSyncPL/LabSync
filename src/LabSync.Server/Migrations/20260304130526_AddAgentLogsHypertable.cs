using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabSync.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentLogsHypertable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentLogs",
                columns: table => new
                {
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CpuUsagePercentage = table.Column<double>(type: "double precision", nullable: true),
                    RamUsageMegabytes = table.Column<double>(type: "double precision", nullable: true),
                    StatusMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLogs", x => new { x.DeviceId, x.Timestamp });
                    table.ForeignKey(
                        name: "FK_AgentLogs_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            //to ja dodalem
            migrationBuilder.Sql(
    "SELECT create_hypertable('\"AgentLogs\"', 'Timestamp', if_not_exists => TRUE);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentLogs");
        }
    }
}
