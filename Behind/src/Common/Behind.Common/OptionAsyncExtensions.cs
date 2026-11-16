using static Behind.Common.FunctionalExtensions;

namespace Behind.Common;

public static class OptionAsyncExtensions
{
    extension<T>(Option<T> option)
    {
        public Task<Option<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> map) =>
            option.Match(
                () => Task.FromResult<Option<TResult>>(None),
                async value => Some(await map(value)));
    }

    extension<T>(Task<Option<T>> option)
    {
        public async Task<Option<TResult>> MapAsync<TResult>(Func<T, Task<TResult>> map) =>
            await (await option).MapAsync(map);
    }
}
