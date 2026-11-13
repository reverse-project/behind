using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Contact.Database.Postgres.Migrations;

/// <inheritdoc />
public partial class _20261113082636_CreateContact : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ContactForms",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                SubjectMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SubjectsJson = table.Column<string>(type: "text", nullable: false),
                FieldsJson = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContactForms", x => new { x.Key, x.Version });
            });

        migrationBuilder.CreateTable(
            name: "ContactRequests",
            columns: table => new
            {
                Key = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                FormId = table.Column<Guid>(type: "uuid", nullable: false),
                FormName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                FieldsJson = table.Column<string>(type: "text", nullable: false),
                DeliveryStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ReadBy = table.Column<Guid>(type: "uuid", nullable: true)
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
