using Unit = System.ValueTuple;

namespace Behind.Common;

public static partial class FunctionalExtensions
{
    public static Exceptional<T> Exceptional<T>(T value) => new(value);
}

public struct Exceptional<T>
{
    internal Exception? Ex { get; }
    internal T? Value { get; }

    public bool Success => Ex == null;
    public bool Exception => Ex != null;

    internal Exceptional(Exception ex)
    {
        Ex = ex ?? throw new ArgumentNullException(nameof(ex));
        Value = default;
    }

    internal Exceptional(T right)
    {
        Value = right;
        Ex = null;
    }

    public static implicit operator Exceptional<T>(Exception left) => new(left);
    public static implicit operator Exceptional<T>(T right) => new(right);

    public TR Match<TR>(Func<Exception, TR> exception, Func<T?, TR> success)
        => Ex is not null && Exception ? exception(Ex) : success(Value);

    public Unit Match(Action<Exception> exception, Action<T?> success)
        => Match(exception.ToFunc(), success.ToFunc());

    public override string ToString()
        => Match(
            ex => $"Exception({ex.Message})",
            t => $"Success({t})");
}

public static class Exceptional
{
    // creating a new Exceptional

    public static Func<T, Exceptional<T>> Return<T>()
        => t => t;

    public static Exceptional<TR> Of<TR>(Exception left) => new(left);

    public static Exceptional<TR> Of<TR>(TR right) => new(right);

    // applicative

    public static Exceptional<TR> Apply<T, TR>(this Exceptional<Func<T, TR>> @this, Exceptional<T> arg)
        => @this.Match(
            exception: ex => ex,
            success: func => arg.Match(
                exception: ex => ex,
                success: t => new Exceptional<TR>(func!(t!))));

    public static Exceptional<Func<T2, TR>> Apply<T1, T2, TR>
        (this Exceptional<Func<T1, T2, TR>> @this, Exceptional<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.Curry!), arg);

    public static Exceptional<Func<T2, T3, TR>> Apply<T1, T2, T3, TR>
        (this Exceptional<Func<T1, T2, T3, TR>> @this, Exceptional<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst!), arg);

    public static Exceptional<Func<T2, T3, T4, TR>> Apply<T1, T2, T3, T4, TR>
        (this Exceptional<Func<T1, T2, T3, T4, TR>> @this, Exceptional<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst!), arg);

    public static Exceptional<Func<T2, T3, T4, T5, TR>> Apply<T1, T2, T3, T4, T5, TR>
        (this Exceptional<Func<T1, T2, T3, T4, T5, TR>> @this, Exceptional<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst!), arg);

    public static Exceptional<Func<T2, T3, T4, T5, T6, TR>> Apply<T1, T2, T3, T4, T5, T6, TR>
        (this Exceptional<Func<T1, T2, T3, T4, T5, T6, TR>> @this, Exceptional<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst!), arg);

    public static Exceptional<Func<T2, T3, T4, T5, T6, T7, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, TR>
        (this Exceptional<Func<T1, T2, T3, T4, T5, T6, T7, TR>> @this, Exceptional<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst!), arg);

    public static Exceptional<Func<T2, T3, T4, T5, T6, T7, T8, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>
        (this Exceptional<Func<T1, T2, T3, T4, T5, T6, T7, T8, TR>> @this, Exceptional<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst!), arg);

    public static Exceptional<Func<T2, T3, T4, T5, T6, T7, T8, T9, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>
        (this Exceptional<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>> @this, Exceptional<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst!), arg);

    // functor

    public static Exceptional<TRr> Map<TR, TRr>(this Exceptional<TR> @this
        , Func<TR?, TRr> func) => @this.Success ? func(@this.Value) : new Exceptional<TRr>(@this.Ex!);

    public static Exceptional<Unit> ForEach<TR>(this Exceptional<TR> @this, Action<TR?> act)
        => Map(@this, act.ToFunc());

    public static Exceptional<TRr> Bind<TR, TRr>(this Exceptional<TR> @this
        , Func<TR?, Exceptional<TRr>> func)
        => @this.Success ? func(@this.Value) : new Exceptional<TRr>(@this.Ex!);

    // LINQ

    public static Exceptional<TR> Select<T, TR>(this Exceptional<T> @this
        , Func<T, TR> map) => @this.Map(map!);

    public static Exceptional<TRr> SelectMany<T, TR, TRr>(this Exceptional<T> @this
        , Func<T?, Exceptional<TR>> bind, Func<T?, TR?, TRr> project)
    {
        if (@this.Exception) return new Exceptional<TRr>(@this.Ex!);
        var bound = bind(@this.Value);
        return bound.Exception
            ? new Exceptional<TRr>(bound.Ex!)
            : project(@this.Value, bound.Value);
    }
}