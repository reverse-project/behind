namespace Behind.Tests.CommonDatabase;

/// <summary>
/// Tests for the default <see cref="IRepository{TAggregate,TKey}.DeleteAsync"/> method.
///
/// NSubstitute does not call default interface-method bodies, so we wrap the
/// substitute in a thin concrete class that delegates every explicit interface
/// method to the mock while leaving <c>DeleteAsync</c> to the interface's
/// default implementation.
/// </summary>
public class IRepositoryDefaultMethodTests
{
    /// <summary>
    /// Forwards all non-default interface members to an inner <see cref="IRepository{T,TKey}"/>
    /// mock.  <c>DeleteAsync</c> is intentionally NOT overridden, so the default
    /// interface implementation runs when called on this type.
    /// </summary>
    private sealed class RepositoryWrapper(IRepository<Email, Guid> inner)
        : IRepository<Email, Guid>
    {
        public Guid GetKey(Email aggregate) => inner.GetKey(aggregate);
        public Task<Option<Email>> TryFindAsync(Guid key, CancellationToken ct) => inner.TryFindAsync(key, ct);
        public Task AddAsync(Email aggregate, CancellationToken ct) => inner.AddAsync(aggregate, ct);
        public void Update(Email aggregate) => inner.Update(aggregate);
        public void Delete(Email aggregate) => inner.Delete(aggregate);
        // DeleteAsync deliberately absent → interface default implementation is used
    }

    // ── DeleteAsync when entity is found ──────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenEntityFound_CallsDeleteOnce()
    {
        var mock = Substitute.For<IRepository<Email, Guid>>();
        IRepository<Email, Guid> repo = new RepositoryWrapper(mock);
        var id = Guid.NewGuid();
        var email = new Email("found@example.com");

        mock.TryFindAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<Email>>(Some(email)));

        await repo.DeleteAsync(id);

        mock.Received(1).Delete(email);
    }

    [Fact]
    public async Task DeleteAsync_WhenEntityFound_CallsDeleteWithCorrectEntity()
    {
        var mock = Substitute.For<IRepository<Email, Guid>>();
        IRepository<Email, Guid> repo = new RepositoryWrapper(mock);
        var id = Guid.NewGuid();
        var email = new Email("target@example.com");

        mock.TryFindAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<Email>>(Some(email)));

        await repo.DeleteAsync(id);

        mock.Received(1).Delete(email);
    }

    // ── DeleteAsync when entity is not found ──────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenEntityNotFound_NeverCallsDelete()
    {
        var mock = Substitute.For<IRepository<Email, Guid>>();
        IRepository<Email, Guid> repo = new RepositoryWrapper(mock);
        var id = Guid.NewGuid();

        mock.TryFindAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<Email>>(None));

        await repo.DeleteAsync(id);

        mock.DidNotReceive().Delete(Arg.Any<Email>());
    }

    [Fact]
    public async Task DeleteAsync_WhenEntityNotFound_CallsTryFindAsync()
    {
        var mock = Substitute.For<IRepository<Email, Guid>>();
        IRepository<Email, Guid> repo = new RepositoryWrapper(mock);
        var id = Guid.NewGuid();

        mock.TryFindAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Option<Email>>(None));

        await repo.DeleteAsync(id);

        await mock.Received(1).TryFindAsync(id, Arg.Any<CancellationToken>());
    }

    // ── CancellationToken is forwarded ────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ForwardsCancellationTokenToTryFindAsync()
    {
        var mock = Substitute.For<IRepository<Email, Guid>>();
        IRepository<Email, Guid> repo = new RepositoryWrapper(mock);
        var id = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        mock.TryFindAsync(id, token)
            .Returns(Task.FromResult<Option<Email>>(None));

        await repo.DeleteAsync(id, token);

        await mock.Received(1).TryFindAsync(id, token);
    }
}
