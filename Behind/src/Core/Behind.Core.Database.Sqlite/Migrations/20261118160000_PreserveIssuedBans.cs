using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Behind.Core.Database.Sqlite.Migrations;

[DbContext(typeof(CoreContextSqlite))]
[Migration("20261118160000_PreserveIssuedBans")]
public sealed class PreserveIssuedBans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "__BanIssuerRetention" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_WallOfShameEntries" PRIMARY KEY,
                "ExpiresAt" INTEGER NULL,
                "Reason" TEXT NOT NULL,
                "UserId" TEXT NOT NULL,
                "GivenById" TEXT NOT NULL,
                CONSTRAINT "FK_WallOfShameEntries_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_WallOfShameEntries_Users_GivenById" FOREIGN KEY ("GivenById") REFERENCES "Users" ("Id") ON DELETE NO ACTION
            );
            INSERT INTO "__BanIssuerRetention" ("Id", "ExpiresAt", "Reason", "UserId", "GivenById")
                SELECT "Id", "ExpiresAt", "Reason", "UserId", "GivenById" FROM "WallOfShameEntries";
            DROP TABLE "WallOfShameEntries";
            ALTER TABLE "__BanIssuerRetention" RENAME TO "WallOfShameEntries";
            CREATE INDEX "IX_WallOfShameEntries_ExpiresAt" ON "WallOfShameEntries" ("ExpiresAt");
            CREATE INDEX "IX_WallOfShameEntries_UserId" ON "WallOfShameEntries" ("UserId");
            CREATE INDEX "IX_WallOfShameEntries_GivenById" ON "WallOfShameEntries" ("GivenById");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "__BanIssuerRetention" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_WallOfShameEntries" PRIMARY KEY,
                "ExpiresAt" INTEGER NULL,
                "Reason" TEXT NOT NULL,
                "UserId" TEXT NOT NULL,
                "GivenById" TEXT NOT NULL,
                CONSTRAINT "FK_WallOfShameEntries_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_WallOfShameEntries_Users_GivenById" FOREIGN KEY ("GivenById") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "__BanIssuerRetention" ("Id", "ExpiresAt", "Reason", "UserId", "GivenById")
                SELECT "Id", "ExpiresAt", "Reason", "UserId", "GivenById" FROM "WallOfShameEntries";
            DROP TABLE "WallOfShameEntries";
            ALTER TABLE "__BanIssuerRetention" RENAME TO "WallOfShameEntries";
            CREATE INDEX "IX_WallOfShameEntries_ExpiresAt" ON "WallOfShameEntries" ("ExpiresAt");
            CREATE INDEX "IX_WallOfShameEntries_UserId" ON "WallOfShameEntries" ("UserId");
            CREATE INDEX "IX_WallOfShameEntries_GivenById" ON "WallOfShameEntries" ("GivenById");
            """);
    }
}
