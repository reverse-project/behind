namespace Behind.Common.Database;

public interface IContentLocaleStore : IContentLocaleContext
{
    string? SelectedContentLocale { get; set; }
    bool ContentLocaleSpecified { get; set; }
    Task<string?> FindContentLocale(string entityType, Guid entityId, CancellationToken cancellationToken);
    Task StageContentLocale(string entityType, Guid entityId, string? locale, CancellationToken cancellationToken);
    IQueryable<TEntity> InContentLocale<TEntity>(
        IQueryable<TEntity> source, string entityType, string? locale) where TEntity : class;
}
