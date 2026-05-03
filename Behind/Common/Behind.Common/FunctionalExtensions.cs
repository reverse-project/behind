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
    
    extension<T>(T? t) where T : class
    {
        public void Null(Action nullAction, Action<T>? notNullAction = null)
        {
            if (t is null)
            {
                nullAction();
            }
            else
            {
                notNullAction?.Invoke(t);
            }
        }
        
        public void NotNull(Action<T> notNullAction, Action? nullAction = null)
        {
            if (t is null)
            {
                nullAction?.Invoke();
            }
            else
            {
                notNullAction.Invoke(t);
            }
        }
    }
    
    extension<T>(T? t) where T : struct
    {
        public void Null(Action nullAction, Action<T>? notNullAction = null)
        {
            if (t.HasValue)
            {
                notNullAction?.Invoke(t.Value);
            }
            else
            {
                nullAction();
            }
        }
        
        public void NotNull(Action<T> notNullAction, Action? nullAction = null)
        {
            if (t.HasValue)
            {
                notNullAction.Invoke(t.Value);
            }
            else
            {
                nullAction?.Invoke();
            }
        }
    }
}