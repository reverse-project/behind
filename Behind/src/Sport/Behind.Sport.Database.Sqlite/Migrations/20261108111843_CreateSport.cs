using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Behind.Sport.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CreateSport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SportDisciplines",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportDisciplines", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "SportPlayers",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Nationality = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PositionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    HeightCm = table.Column<int>(type: "INTEGER", nullable: true),
                    WeightKg = table.Column<int>(type: "INTEGER", nullable: true),
                    DominantSide = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Biography = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportPlayers", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "SportPlayerSeasons",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeasonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShirtNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    PictureId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportPlayerSeasons", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "SportPositions",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportPositions", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "SportSeasons",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportSeasons", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "SportStats",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StatTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportStats", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "SportStatTypes",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    HigherIsBetter = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportStatTypes", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "SportTeams",
                columns: table => new
                {
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FoundedYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LogoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportTeams", x => new { x.Key, x.Version });
                });

            migrationBuilder.CreateIndex(
                name: "IX_SportDisciplines_Slug",
                table: "SportDisciplines",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_SportPlayers_DisciplineId",
                table: "SportPlayers",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_SportPlayers_PositionId",
                table: "SportPlayers",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_SportPlayerSeasons_PlayerId",
                table: "SportPlayerSeasons",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_SportPlayerSeasons_SeasonId",
                table: "SportPlayerSeasons",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SportPlayerSeasons_TeamId",
                table: "SportPlayerSeasons",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SportPositions_DisciplineId",
                table: "SportPositions",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_SportSeasons_DisciplineId",
                table: "SportSeasons",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_SportStats_SeasonId_StatTypeId",
                table: "SportStats",
                columns: new[] { "SeasonId", "StatTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_SportStats_SubjectId",
                table: "SportStats",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SportStatTypes_DisciplineId",
                table: "SportStatTypes",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_SportTeams_DisciplineId",
                table: "SportTeams",
                column: "DisciplineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SportDisciplines");

            migrationBuilder.DropTable(
                name: "SportPlayers");

            migrationBuilder.DropTable(
                name: "SportPlayerSeasons");

            migrationBuilder.DropTable(
                name: "SportPositions");

            migrationBuilder.DropTable(
                name: "SportSeasons");

            migrationBuilder.DropTable(
                name: "SportStats");

            migrationBuilder.DropTable(
                name: "SportStatTypes");

            migrationBuilder.DropTable(
                name: "SportTeams");
        }
    }
}
