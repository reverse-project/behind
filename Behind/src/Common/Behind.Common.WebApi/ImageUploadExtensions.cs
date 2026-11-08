using Behind.Common.Database.Modules;
using Microsoft.AspNetCore.Http;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.WebApi;

public sealed record ImageUploadResponse(Guid Id, string Url);

public sealed record ImageLimitResponse(long MaxBytes);

public static class ImageUploadExtensions
{
    private const string FallbackFileName = "image";

    extension(HttpContext httpContext)
    {
        public async Task<IResult> StoreImage<TUploaded, TLimit>(
            IFormFile? file,
            long limit,
            Func<IModuleFiles.Upload, Task<Guid>> add,
            Func<Guid, TUploaded> uploaded,
            Func<long, TLimit> exceeded,
            string type)
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest();
            }

            if (file.Length > limit)
            {
                return Results.Json(exceeded(limit), statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            var content = await file.ReadAllBytesAsync(httpContext.RequestAborted);

            return await content.DetectImageContentType().Match(
                () => Task.FromResult<IResult>(Results.StatusCode(StatusCodes.Status415UnsupportedMediaType)),
                async contentType =>
                {
                    var id = await add(new IModuleFiles.Upload(
                        type,
                        file.FileName.ToSafeFileName(FallbackFileName, IModuleFiles.MaxFileNameLength),
                        contentType,
                        content));

                    return Results.Json(uploaded(id));
                });
        }

        public IResult ServeImage(Option<IModuleFiles.FileContent> image) =>
            image.Match(
                () => Results.NotFound(),
                found => httpContext.ToImmutableImage(found.ContentType, found.Bytes));
    }
}
