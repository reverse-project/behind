using Unit = System.ValueTuple;
using System.Collections.Immutable;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common;

public static class EnumerableExtensions
{
    public static Func<T, IEnumerable<T>> Return<T>() => t => List(t);

    public static IEnumerable<T> Append<T>(this IEnumerable<T> source
        , params T[] ts) => source.Concat(ts);

    static IEnumerable<T> Prepend<T>(this IEnumerable<T> source, T val)
    {
        yield return val;
        foreach (T t in source)
        {
            yield return t;
        }
    }

    public static Option<T> Find<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        => source.Where(predicate).Head();

    public static T FirstOr<T>(this IEnumerable<T> source, T defaultValue)
        => source.Head().Match(
            () => defaultValue,
            t => t);

    public static IEnumerable<Unit> ForEach<T>
        (this IEnumerable<T> ts, Action<T> action)
        => ts.Map(action.ToFunc()).ToImmutableList();

    public static IEnumerable<TR> Map_InTermsOfFold<T, TR>
        (this IEnumerable<T> ts, Func<T, TR> f)
        => ts.Aggregate(List<TR>()
            , (rs, t) => rs.Append(f(t)));

    static IEnumerable<T> Where_InTermsOfFold<T>
        (this IEnumerable<T> @this, Func<T, bool> predicate)
        => @this.Aggregate(List<T>()
            , (ts, t) => predicate(t) ? ts.Append(t) : ts);

    static IEnumerable<TR> Bind_InTermsOfFold<T, TR>
        (this IEnumerable<T> ts, Func<T, IEnumerable<TR>> f)
        => ts.Aggregate(List<TR>()
            , (rs, t) => rs.Concat(f(t)));

    public static IEnumerable<TR> Map<T, TR>
        (this IEnumerable<T> list, Func<T, TR> func)
        => list.Select(func);

    public static TR Match<T, TR>(this IEnumerable<T> list
        , Func<TR> empty, Func<T, IEnumerable<T>, TR> otherwise)
        // ReSharper disable once PossibleMultipleEnumeration
        => list.Head().Match(
            none: empty,
            // ReSharper disable once PossibleMultipleEnumeration
            some: head => otherwise(head, list.Skip(1)));

    public static Option<T> Head<T>(this IEnumerable<T>? list)
    {
        if (list is null)
        {
            return None;
        }

        using var enumerator = list.GetEnumerator();
        return enumerator.MoveNext() ? Some(enumerator.Current) : None;
    }
    public static IEnumerable<Func<T2, TR>> Map<T1, T2, TR>(this IEnumerable<T1> list
        , Func<T1, T2, TR> func)
        => list.Map(func.Curry());

    public static IEnumerable<Func<T2, Func<T3, TR>>>
        Map<T1, T2, T3, TR>(this IEnumerable<T1> opt, Func<T1, T2, T3, TR> func)
        => opt.Map(func.Curry());

    public static IEnumerable<TR> Bind<T, TR>(this IEnumerable<T> list, Func<T, IEnumerable<TR>> func)
        => list.SelectMany(func);

    public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> list)
        => list.SelectMany(x => x);

    public static IEnumerable<TR> Bind<T, TR>(this IEnumerable<T> list, Func<T, Option<TR>> func)
        => list.Bind(t => func(t).AsEnumerable());

    // LINQ

    public static IEnumerable<TRr> SelectMany<T, TR, TRr>
    (this IEnumerable<T> source
        , Func<T, Option<TR>> bind
        , Func<T, TR, TRr> project)
        => from t in source
            let opt = bind(t)
            where opt.IsSome()
            select project(t, opt.ValueUnsafe());

    static IEnumerable<T> TakeWhile<T>(this IEnumerable<T> @this, Func<T, bool> pred)
    {
        foreach (var item in @this)
        {
            if (!pred(item))
            {
                yield break;
            }

            yield return item;
        }
    }

    static IEnumerable<T> DropWhile<T>(this IEnumerable<T> @this, Func<T, bool> pred)
    {
        bool clean = true;
        foreach (var item in @this)
        {
            if (!clean || !pred(item))
            {
                yield return item;
                clean = false;
            }
        }
    }
}