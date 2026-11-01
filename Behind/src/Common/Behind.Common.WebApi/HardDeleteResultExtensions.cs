using Behind.Common.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi;

public static class HardDeleteResultExtensions
{
    extension(HardDeleteResult result)
    {
        public IResult ToResult() =>
            result switch
            {
                HardDeleteResult.Deleted => Results.NoContent(),
                HardDeleteResult.NotFound => Results.NotFound(),
                _ => Results.Conflict()
            };
    }
}
