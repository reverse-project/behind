using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Core.Database.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261115232156_AddSchedules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScheduleRuns",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                ScheduleId = table.Column<Guid>(type: "TEXT", nullable: false),
                ScheduleName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                ScheduledFor = table.Column<long>(type: "INTEGER", nullable: false),
                StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                Link = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                Email = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Recipients = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduleRuns", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "Schedules",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Cron = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Parameters = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                Notify = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Participants = table.Column<string>(type: "TEXT", maxLength: 50000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Schedules", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_ScheduleRuns_ScheduleId",
            table: "ScheduleRuns",
            column: "ScheduleId");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduleRuns_StartedAt",
            table: "ScheduleRuns",
            column: "StartedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduleRuns_Status",
            table: "ScheduleRuns",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_Schedules_Name",
            table: "Schedules",
            column: "Name");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ScheduleRuns");

        migrationBuilder.DropTable(
            name: "Schedules");
    }
}
