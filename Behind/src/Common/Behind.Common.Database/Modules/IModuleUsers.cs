namespace Behind.Common.Database.Modules;

public interface IModuleUsers
{
    public sealed record Profile(Guid Id, string DisplayName, Guid? PictureId);

    Task<IReadOnlyList<Profile>> Find(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
