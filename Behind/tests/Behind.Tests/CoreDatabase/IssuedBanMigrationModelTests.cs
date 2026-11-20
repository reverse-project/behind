using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Postgres = Behind.Core.Database.Postgres.Migrations;
using Sqlite = Behind.Core.Database.Sqlite.Migrations;

namespace Behind.Tests.CoreDatabase;

public sealed class IssuedBanMigrationModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TargetModelsRetainTheIssuerRelationshipBeforeAndAfterMigration(bool postgres)
    {
        Migration previous = postgres ? new Postgres.RoleNameUniqueness() : new Sqlite.RoleNameUniqueness();
        Migration current = postgres ? new Postgres.PreserveIssuedBans() : new Sqlite.PreserveIssuedBans();

        Assert.Equal(DeleteBehavior.Cascade, IssuerDeleteBehavior(previous));
        Assert.Equal(DeleteBehavior.ClientNoAction, IssuerDeleteBehavior(current));
    }

    private static DeleteBehavior IssuerDeleteBehavior(Migration migration)
    {
        var ban = migration.TargetModel.FindEntityType(typeof(WallOfShameEntry).FullName!)!;
        return ban.GetForeignKeys().Single(key => key.Properties.Single().Name == "GivenById").DeleteBehavior;
    }
}
