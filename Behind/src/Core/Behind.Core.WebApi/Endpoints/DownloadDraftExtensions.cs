using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public static class DownloadDraftExtensions
{
    extension(DownloadEndpoints.SaveFolderRequest request)
    {
        public Option<IDownloadFolderCommands.FolderDraft> ToDraft()
        {
            var displayName = (request.DisplayName ?? string.Empty).Trim();
            var source = string.IsNullOrWhiteSpace(request.Slug) ? displayName : request.Slug;

            return source.ToSlug(DownloadFolder.MaxSlugLength).Bind(slug =>
                displayName.Length == 0
                    ? None
                    : Some(new IDownloadFolderCommands.FolderDraft(slug, displayName)));
        }
    }
}
