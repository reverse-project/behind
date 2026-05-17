using Unit = System.ValueTuple;

namespace Behind.Common;

using static FunctionalExtensions;

public static partial class FunctionalExtensions
{
    public static Task<T> Async<T>(T t) => Task.FromResult(t);
}

public static class TaskExtensions
{
    public static async Task<TR> Apply<T, TR>
        (this Task<Func<T, TR>> f, Task<T> arg)
        //=> (await f)(await arg);
        => (await f.ConfigureAwait(false))(await arg.ConfigureAwait(false));

    public static Task<Func<T2, TR>> Apply<T1, T2, TR>
        (this Task<Func<T1, T2, TR>> f, Task<T1> arg)
        => Apply(f.Map(FunctionalExtensions.Curry), arg);

    public static Task<Func<T2, T3, TR>> Apply<T1, T2, T3, TR>
        (this Task<Func<T1, T2, T3, TR>> @this, Task<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Task<Func<T2, T3, T4, TR>> Apply<T1, T2, T3, T4, TR>
        (this Task<Func<T1, T2, T3, T4, TR>> @this, Task<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Task<Func<T2, T3, T4, T5, TR>> Apply<T1, T2, T3, T4, T5, TR>
        (this Task<Func<T1, T2, T3, T4, T5, TR>> @this, Task<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Task<Func<T2, T3, T4, T5, T6, TR>> Apply<T1, T2, T3, T4, T5, T6, TR>
        (this Task<Func<T1, T2, T3, T4, T5, T6, TR>> @this, Task<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Task<Func<T2, T3, T4, T5, T6, T7, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, TR>
        (this Task<Func<T1, T2, T3, T4, T5, T6, T7, TR>> @this, Task<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Task<Func<T2, T3, T4, T5, T6, T7, T8, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>
        (this Task<Func<T1, T2, T3, T4, T5, T6, T7, T8, TR>> @this, Task<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Task<Func<T2, T3, T4, T5, T6, T7, T8, T9, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>
        (this Task<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>> @this, Task<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static async Task<TR> Map<T, TR>
        (this Task<T> task, Func<T, TR> f)
        //=> f(await task);
        => f(await task.ConfigureAwait(false));

    public static async Task<TR> Map<TR>
        (this Task task, Func<TR> f)
    {
        await task;
        return f();
    }

    public static Task<Func<T2, TR>> Map<T1, T2, TR>
        (this Task<T1> @this, Func<T1, T2, TR> func)
        => @this.Map(func.Curry());

    public static Task<Func<T2, T3, TR>> Map<T1, T2, T3, TR>
        (this Task<T1> @this, Func<T1, T2, T3, TR> func)
        => @this.Map(func.CurryFirst());

    public static Task<Func<T2, T3, T4, TR>> Map<T1, T2, T3, T4, TR>
        (this Task<T1> @this, Func<T1, T2, T3, T4, TR> func)
        => @this.Map(func.CurryFirst());

    public static Task<Func<T2, T3, T4, T5, TR>> Map<T1, T2, T3, T4, T5, TR>
        (this Task<T1> @this, Func<T1, T2, T3, T4, T5, TR> func)
        => @this.Map(func.CurryFirst());

    public static Task<Func<T2, T3, T4, T5, T6, TR>> Map<T1, T2, T3, T4, T5, T6, TR>
        (this Task<T1> @this, Func<T1, T2, T3, T4, T5, T6, TR> func)
        => @this.Map(func.CurryFirst());

    public static Task<Func<T2, T3, T4, T5, T6, T7, TR>> Map<T1, T2, T3, T4, T5, T6, T7, TR>
        (this Task<T1> @this, Func<T1, T2, T3, T4, T5, T6, T7, TR> func)
        => @this.Map(func.CurryFirst());

    public static Task<Func<T2, T3, T4, T5, T6, T7, T8, TR>> Map<T1, T2, T3, T4, T5, T6, T7, T8, TR>
        (this Task<T1> @this, Func<T1, T2, T3, T4, T5, T6, T7, T8, TR> func)
        => @this.Map(func.CurryFirst());

    public static Task<Func<T2, T3, T4, T5, T6, T7, T8, T9, TR>> Map<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>
        (this Task<T1> @this, Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR> func)
        => @this.Map(func.CurryFirst());

    public static Task<TR> Map<T, TR>
        (this Task<T> task, Func<Exception, TR> faulted, Func<T, TR> completed)
        => task.ContinueWith(t =>
            t.Status == TaskStatus.Faulted
                ? faulted(t.Exception!)
                : completed(t.Result));

    public static Task<Unit> ForEach<T>(this Task<T> @this, Action<T> continuation)
        => @this.ContinueWith(t => continuation.ToFunc()(t.Result)
            , TaskContinuationOptions.OnlyOnRanToCompletion);

    public static async Task<TR> Bind<T, TR>
        (this Task<T> task, Func<T, Task<TR>> f)
        //=> await f(await task);
        => await f(await task.ConfigureAwait(false)).ConfigureAwait(false);


    public static Task<T> OrElse<T>
        (this Task<T> task, Func<Task<T>> fallback)
        => task.ContinueWith(t =>
                t.Status == TaskStatus.Faulted
                    ? fallback()
                    : Task.FromResult(t.Result)
            )
            .Unwrap();


    public static Task<T> Recover<T>
        (this Task<T> task, Func<Exception, T> fallback)
        => task.ContinueWith(t =>
            t.Status == TaskStatus.Faulted
                ? fallback(t.Exception!)
                : t.Result);

    public static Task<T> RecoverWith<T>
        (this Task<T> task, Func<Exception, Task<T>> fallback)
        => task.ContinueWith(t =>
            t.Status == TaskStatus.Faulted
                ? fallback(t.Exception!)
                : Task.FromResult(t.Result)
        ).Unwrap();

    // LINQ

    public static async Task<TRr> SelectMany<T, TR, TRr>
        (this Task<T> task, Func<T, Task<TR>> bind, Func<T, TR, TRr> project)
    {
        T t = await task;
        TR r = await bind(t);
        return project(t, r);
    }

    public static async Task<TRr> SelectMany<TR, TRr>
        (this Task task, Func<Unit, Task<TR>> bind, Func<Unit, TR, TRr> project)
    {
        await task;
        TR r = await bind(Unit());
        return project(Unit(), r);
    }

    public static async Task<TR> Select<T, TR>(this Task<T> task, Func<T, TR> f)
        => f(await task);

    public static async Task<T> Where<T>(this Task<T> source
        , Func<T, bool> predicate)
    {
        T t = await source;
        if (!predicate(t)) throw new OperationCanceledException();
        return t;
    }

    public static async Task<TV> Join<T, TU, TK, TV>(
        this Task<T> source, Task<TU> inner,
        Func<T, TK> outerKeySelector, Func<TU, TK> innerKeySelector,
        Func<T, TU, TV> resultSelector)
    {
        await Task.WhenAll(source, inner);
        if (!EqualityComparer<TK>.Default.Equals(
                outerKeySelector(source.Result), innerKeySelector(inner.Result)))
            throw new OperationCanceledException();
        return resultSelector(source.Result, inner.Result);
    }

    public static async Task<TV> GroupJoin<T, TU, TK, TV>(
        this Task<T> source, Task<TU> inner,
        Func<T, TK> outerKeySelector, Func<TU, TK> innerKeySelector,
        Func<T, Task<TU>, TV> resultSelector)
    {
        T t = await source;
        return resultSelector(t,
            inner.Where(u => EqualityComparer<TK>.Default.Equals(
                outerKeySelector(t), innerKeySelector(u))));
    }

    //public static async Task<T> Cast<T>(this Task source)
    //{
    //   await source;
    //   return (T)((dynamic)source).Result;
    //}
}