using System.Collections.Immutable;
using System.Reflection;
using Unit = System.ValueTuple;

namespace Behind.Common;

public static partial class FunctionalExtensions
{
    public static Unit Unit() => default;

    // function manipulation 

    public static Func<T1, Func<T2, TR>> Curry<T1, T2, TR>(this Func<T1, T2, TR> func)
        => t1 => t2 => func(t1, t2);

    public static Func<T1, Func<T2, Func<T3, TR>>> Curry<T1, T2, T3, TR>(this Func<T1, T2, T3, TR> func)
        => t1 => t2 => t3 => func(t1, t2, t3);

    public static Func<T1, Func<T2, T3, TR>> CurryFirst<T1, T2, T3, TR>
        (this Func<T1, T2, T3, TR> @this) => t1 => (t2, t3) => @this(t1, t2, t3);

    public static Func<T1, Func<T2, T3, T4, TR>> CurryFirst<T1, T2, T3, T4, TR>
        (this Func<T1, T2, T3, T4, TR> @this) => t1 => (t2, t3, t4) => @this(t1, t2, t3, t4);

    public static Func<T1, Func<T2, T3, T4, T5, TR>> CurryFirst<T1, T2, T3, T4, T5, TR>
        (this Func<T1, T2, T3, T4, T5, TR> @this) => t1 => (t2, t3, t4, t5) => @this(t1, t2, t3, t4, t5);

    public static Func<T1, Func<T2, T3, T4, T5, T6, TR>> CurryFirst<T1, T2, T3, T4, T5, T6, TR>
        (this Func<T1, T2, T3, T4, T5, T6, TR> @this) => t1 => (t2, t3, t4, t5, t6) => @this(t1, t2, t3, t4, t5, t6);

    public static Func<T1, Func<T2, T3, T4, T5, T6, T7, TR>> CurryFirst<T1, T2, T3, T4, T5, T6, T7, TR>
        (this Func<T1, T2, T3, T4, T5, T6, T7, TR> @this) =>
        t1 => (t2, t3, t4, t5, t6, t7) => @this(t1, t2, t3, t4, t5, t6, t7);

    public static Func<T1, Func<T2, T3, T4, T5, T6, T7, T8, TR>> CurryFirst<T1, T2, T3, T4, T5, T6, T7, T8, TR>
        (this Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> @this) =>
        t1 => (t2, t3, t4, t5, t6, t7, t8) => @this(t1, t2, t3, t4, t5, t6, t7, t8);

    public static Func<T1, Func<T2, T3, T4, T5, T6, T7, T8, T9, TR>> CurryFirst<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>
        (this Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> @this) => t1 =>
        (t2, t3, t4, t5, t6, t7, t8, t9) => @this(t1, t2, t3, t4, t5, t6, t7, t8, t9);

    public static Func<T, T> Tap<T>(Action<T> act)
        => x =>
        {
            act(x);
            return x;
        };

    public static TR Pipe<T, TR>(this T @this, Func<T, TR> func) => func(@this);

    /// <summary>
    /// Pipes the input value in the given Action, i.e. invokes the given Action on the given value.
    /// returning the input value. Not really a genuine implementation of pipe, since it combines pipe with Tap.
    /// </summary>
    public static T Pipe<T>(this T input, Action<T> func) => Tap(func)(input);

    // DATA STRUCTURES

    public static KeyValuePair<TK, T> Pair<TK, T>(TK key, T value)
        => new KeyValuePair<TK, T>(key, value);

    public static IEnumerable<T> List<T>(params T[] items) => items.ToImmutableList();

    public static Func<T, IEnumerable<T>> SingletonList<T>() => (item) => ImmutableList.Create(item);

    public static IEnumerable<T> Cons<T>(this T t, IEnumerable<T> ts)
        => List(t).Concat(ts);

    public static Func<T, IEnumerable<T>, IEnumerable<T>> Cons<T>()
        => (t, ts) => t.Cons(ts);

    public static IDictionary<TK, T> Map<TK, T>(params KeyValuePair<TK, T>[] pairs) where TK : notnull => pairs.ToImmutableDictionary();

    // misc

    // Using
    public static TR Using<TDisp, TR>(TDisp disposable
        , Func<TDisp, TR> func) where TDisp : IDisposable
    {
        using (var disp = disposable)
        {
            return func(disp);
        }
    }

    public static Unit Using<TDisp>(TDisp disposable
        , Action<TDisp> act) where TDisp : IDisposable
        => Using(disposable, act.ToFunc());

    public static TR Using<TDisp, TR>(Func<TDisp> createDisposable
        , Func<TDisp, TR> func) where TDisp : IDisposable
    {
        using (var disp = createDisposable())
        {
            return func(disp);
        }
    }

    public static Unit Using<TDisp>(Func<TDisp> createDisposable
        , Action<TDisp> action) where TDisp : IDisposable
        => Using(createDisposable, action.ToFunc());

    // Range
    public static IEnumerable<char> Range(char from, char to)
    {
        for (var i = from; i <= to; i++)
        {
            yield return i;
        }
    }

    public static IEnumerable<int> Range(int from, int to)
    {
        for (var i = from; i <= to; i++)
        {
            yield return i;
        }
    }

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


    extension<T>(T t)
    {
        public bool AreEqual(T other)
        {
            if (ReferenceEquals(t, other))
            {
                return true;
            }

            if (t is null || other is null)
            {
                return false;
            }

            return t.Equals(other);
        }
    }
    
    extension<T>(T t) where T : notnull
    {
        public IReadOnlyList<PropertyDifference> GetDifferences(T other)
        {
            var properties = typeof(T)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead);

            return properties
                .Select(property =>
                {
                    var oldPropertyValue = property.GetValue(t);
                    var newPropertyValue = property.GetValue(other);

                    return new
                    {
                        property.Name,
                        OldValue = oldPropertyValue,
                        NewValue = newPropertyValue,
                        IsDifferent = !oldPropertyValue.AreEqual(newPropertyValue)
                    };
                })
                .Where(x => x.IsDifferent)
                .Select(x => new PropertyDifference(
                    x.Name,
                    x.OldValue,
                    x.NewValue
                ))
                .ToList();
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
        public Option<T> ToOption() => t.HasValue ? Some(t.Value) : None;

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

    extension(bool? b)
    {
        public bool IsTrue() => b is true;
    }
}