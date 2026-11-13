using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Contact.Database.Sqlite.Migrations;

/// <inheritdoc />
public partial class _20261113082633_CreateContact : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ContactForms",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                Recipient = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                SubjectMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                SubjectsJson = table.Column<string>(type: "TEXT", nullable: false),
                FieldsJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContactForms", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "ContactRequests",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false),
                FormId = table.Column<Guid>(type: "TEXT", nullable: false),
                FormName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Recipient = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                Subject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                FieldsJson = table.Column<string>(type: "TEXT", nullable: false),
                DeliveryStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                ReadBy = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContactRequests", x => new { x.Key, x.Version });
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ContactForms");

        migrationBuilder.DropTable(
            name: "ContactRequests");
    }
}
