using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Behind.Common.WebApi;

public static class FormFileExtensions
{
    extension(IFormFile file)
    {
        public string ToStoredContentType(int maxLength) =>
            MediaTypeHeaderValue.TryParse(file.ContentType, out var parsed)
            && parsed.MediaType.Value is { Length: > 0 } mediaType
            && mediaType.Length <= maxLength
                ? mediaType
                : AttachmentResponseExtensions.DefaultContentType;

        public async Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
        {
            await using var stream = file.OpenReadStream();
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);

            return buffer.ToArray();
        }
    }
}
