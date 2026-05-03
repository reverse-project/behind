using LaYumba.Functional;

namespace Behind.Common;

public static class FunctionalExtensions
{
    extension<T>(Try<T> @try)
    {
        Exceptional<T>? Finally(Action<Exceptional<T>?> action)
        {
            Exceptional<T>? result = null;
            try
            {
                result = @try();
                return result;
            }
            finally
            {
                if (result is not null)
                {
                    action(result);
                }
            }
        }
    }

    public static async Task<Exceptional<T>> TryAsync<T>(Func<Task<T>> func)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}