using Behind.Core.Database;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Identity;

namespace Behind.Tests.CoreWebApi.Identity;

public class IdentityStoreLoggingTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IEmailQueries _emailQueries = Substitute.For<IEmailQueries>();
    private readonly CapturingLogger<IdentityStore> _logger = new();
    private readonly IdentityStore _store;

    public IdentityStoreLoggingTests()
    {
        _unitOfWork.GetRepository<HumanUser, Guid>().Returns(_ => throw new InvalidOperationException("db"));
        _unitOfWork.GetRepository<UserRole, Guid>().Returns(_ => throw new InvalidOperationException("db"));
        _emailQueries.GetEmailByValue(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Option<Email>>(new InvalidOperationException("db")));

        _store = StoreWith(_unitOfWork);
    }

    private IdentityStore StoreWith(IUnitOfWork unitOfWork) =>
        new(
            unitOfWork,
            Substitute.For<IUserQueries>(),
            _emailQueries,
            Substitute.For<IEmailCommands>(),
            Substitute.For<IPasswordQueries>(),
            Substitute.For<IRoleQueries>(),
            Substitute.For<IPasswordCommands>(),
            Substitute.For<IUserCommands>(),
            Substitute.For<IUserApprovalQueries>(),
            Substitute.For<IUserApprovalCommands>(),
            Substitute.For<IRoleCommands>(),
            _logger);

    private static ApplicationUser NewUser() =>
        new() { DisplayName = "User", Emails = [new Email("user@test.com")] };

    private void AssertErrorFor(string key, object expected)
    {
        var entry = _logger.Single(LogLevel.Error);
        Assert.Equal(expected, entry.State[key]);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task CreateUser_DatabaseFailure_LogsErrorWithUserId()
    {
        var user = NewUser();

        var result = await _store.CreateAsync(user, CancellationToken.None);

        Assert.False(result.Succeeded);
        AssertErrorFor("UserId", user.Id);
    }

    [Fact]
    public async Task UpdateUser_DatabaseFailure_LogsErrorWithUserId()
    {
        var user = NewUser();

        var result = await _store.UpdateAsync(user, CancellationToken.None);

        Assert.False(result.Succeeded);
        AssertErrorFor("UserId", user.Id);
    }

    [Fact]
    public async Task DeleteUser_DatabaseFailure_LogsErrorWithUserId()
    {
        var user = NewUser();

        var result = await _store.DeleteAsync(user, CancellationToken.None);

        Assert.False(result.Succeeded);
        AssertErrorFor("UserId", user.Id);
    }

    [Fact]
    public async Task FindUserById_DatabaseFailure_LogsErrorWithUserId()
    {
        var id = Guid.NewGuid().ToString();

        Assert.Null(await _store.FindByIdAsync(id, CancellationToken.None));

        AssertErrorFor("UserId", id);
    }

    [Fact]
    public async Task FindUserById_InvalidIdentifier_LogsDebugOnly()
    {
        Assert.Null(await _store.FindByIdAsync("not-a-guid", CancellationToken.None));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("not-a-guid", entry.State["UserId"]);
    }

    [Fact]
    public async Task CreateRole_DatabaseFailure_LogsErrorWithRoleId()
    {
        var role = new ApplicationRole { Name = "Role" };

        var result = await _store.CreateAsync(role, CancellationToken.None);

        Assert.False(result.Succeeded);
        AssertErrorFor("RoleId", role.Id);
    }

    [Fact]
    public async Task UpdateRole_DatabaseFailure_LogsErrorWithRoleId()
    {
        var role = new ApplicationRole { Name = "Role" };

        await _store.UpdateAsync(role, CancellationToken.None);

        AssertErrorFor("RoleId", role.Id);
    }

    [Fact]
    public async Task DeleteRole_DatabaseFailure_LogsErrorWithRoleId()
    {
        var role = new ApplicationRole { Name = "Role" };

        await _store.DeleteAsync(role, CancellationToken.None);

        AssertErrorFor("RoleId", role.Id);
    }

    [Fact]
    public async Task FindRoleById_DatabaseFailure_LogsErrorWithRoleId()
    {
        var id = Guid.NewGuid().ToString();

        Assert.Null(await ((IRoleStore<ApplicationRole>)_store).FindByIdAsync(id, CancellationToken.None));

        AssertErrorFor("RoleId", id);
    }

    private IdentityStore CancellingStore()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.GetRepository<HumanUser, Guid>().Returns(_ => throw new OperationCanceledException());
        unitOfWork.GetRepository<UserRole, Guid>().Returns(_ => throw new OperationCanceledException());

        return StoreWith(unitOfWork);
    }

    [Fact]
    public async Task UpdateUser_Cancelled_PropagatesWithoutLoggingAnError()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CancellingStore().UpdateAsync(NewUser(), CancellationToken.None));

        Assert.False(_logger.Has(LogLevel.Error));
    }

    [Fact]
    public async Task FindRoleById_Cancelled_PropagatesWithoutLoggingAnError()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ((IRoleStore<ApplicationRole>)CancellingStore())
                .FindByIdAsync(Guid.NewGuid().ToString(), CancellationToken.None));

        Assert.False(_logger.Has(LogLevel.Error));
    }

    [Fact]
    public async Task FindRoleById_InvalidIdentifier_LogsDebugOnly()
    {
        Assert.Null(await ((IRoleStore<ApplicationRole>)_store).FindByIdAsync("nope", CancellationToken.None));

        Assert.Equal(LogLevel.Debug, Assert.Single(_logger.Entries).Level);
    }
}
