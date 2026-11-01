using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi;

public static class FormFileExtensions
{
    extension(IFormFile file)
    {
        public async Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
        {
            await using var stream = file.OpenReadStream();
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);

            return buffer.ToArray();
        }
    }
}
