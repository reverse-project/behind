namespace Behind.Core.WebApi.Layouts;

public interface ILayoutService
{
    Task<LayoutConfiguration> Read(CancellationToken cancellationToken);
    Task<LayoutSaveResult> Save(LayoutConfiguration configuration, bool manageDeleted, CancellationToken cancellationToken);
}
