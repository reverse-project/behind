using Unit = System.ValueTuple;

namespace Behind.Common;

using static FunctionalExtensions;

public static partial class FunctionalExtensions
{
    public static Validation<T> Valid<T>(T value) => new(value);

    // create a Validation in the Invalid state
    public static Validation.Invalid Invalid(params Error[] errors) => new(errors);
    public static Validation<TR> Invalid<TR>(params Error[] errors) => new Validation.Invalid(errors);
    public static Validation.Invalid Invalid(IEnumerable<Error> errors) => new(errors);
    public static Validation<TR> Invalid<TR>(IEnumerable<Error> errors) => new Validation.Invalid(errors);
}

public readonly struct Validation<T> : IEquatable<Validation<T>>
{
    internal IEnumerable<Error> Errors { get; }
    private readonly T? _value;

    public bool IsValid { get; }

    // the Return function for Validation
    public static Func<T, Validation<T>> Return = Valid;

    public static Validation<T> Fail(IEnumerable<Error> errors) => new(errors);

    public static Validation<T> Fail(params Error[] errors) => new(errors.AsEnumerable());

    private Validation(IEnumerable<Error> errors)
    {
        IsValid = false;
        Errors = errors;
        _value = default;
    }

    internal Validation(T right)
    {
        IsValid = true;
        _value = right;
        Errors = Enumerable.Empty<Error>();
    }

    public static implicit operator Validation<T>(Error error)
        => new([error]);

    public static implicit operator Validation<T>(Validation.Invalid left)
        => new(left.Errors);

    public static implicit operator Validation<T>(T right) => Valid(right);

    public TR Match<TR>(Func<IEnumerable<Error>, TR> invalid, Func<T, TR> valid)
        => IsValid && _value is not null ? valid(_value) : invalid(Errors);

    public Unit Match(Action<IEnumerable<Error>> invalid, Action<T> valid)
        => Match(invalid.ToFunc(), valid.ToFunc());

    public IEnumerator<T?> AsEnumerable()
    {
        if (IsValid)
        {
            yield return _value;
        }
    }

    public override string ToString()
        => IsValid
            ? $"Valid({_value})"
            : $"Invalid([{string.Join(", ", Errors)}])";

    public override bool Equals(object? obj) => ToString() == obj?.ToString(); // hack

    public bool Equals(Validation<T> other)
    {
        return IsValid == other.IsValid
               && Errors.SequenceEqual(other.Errors)
               && EqualityComparer<T?>.Default.Equals(_value, other._value);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(IsValid);
        hash.Add(_value);

        foreach (var error in Errors)
        {
            hash.Add(error);
        }

        return hash.ToHashCode();
    }
}

public static class Validation
{
    public struct Invalid
    {
        internal IEnumerable<Error> Errors;

        public Invalid(IEnumerable<Error> errors)
        {
            Errors = errors;
        }
    }

    public static T GetOrElse<T>(this Validation<T> opt, T defaultValue)
        => opt.Match(
            _ => defaultValue,
            t => t);

    public static T GetOrElse<T>(this Validation<T> opt, Func<T> fallback)
        => opt.Match(
            _ => fallback(),
            t => t);

    public static Validation<TR> Apply<T, TR>(this Validation<Func<T, TR>> valF, Validation<T> valT)
        => valF.Match(
            valid: f => valT.Match(
                valid: t => Valid(f(t)),
                invalid: Invalid<TR>),
            invalid: errF => valT.Match(
                valid: _ => Invalid<TR>(errF),
                invalid: errT => Invalid<TR>(errF.Concat(errT))));

    public static Validation<Func<T2, TR>> Apply<T1, T2, TR>
        (this Validation<Func<T1, T2, TR>> @this, Validation<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.Curry), arg);

    public static Validation<Func<T2, T3, TR>> Apply<T1, T2, T3, TR>
        (this Validation<Func<T1, T2, T3, TR>> @this, Validation<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Validation<Func<T2, T3, T4, TR>> Apply<T1, T2, T3, T4, TR>
        (this Validation<Func<T1, T2, T3, T4, TR>> @this, Validation<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Validation<Func<T2, T3, T4, T5, TR>> Apply<T1, T2, T3, T4, T5, TR>
        (this Validation<Func<T1, T2, T3, T4, T5, TR>> @this, Validation<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Validation<Func<T2, T3, T4, T5, T6, TR>> Apply<T1, T2, T3, T4, T5, T6, TR>
        (this Validation<Func<T1, T2, T3, T4, T5, T6, TR>> @this, Validation<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Validation<Func<T2, T3, T4, T5, T6, T7, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, TR>
        (this Validation<Func<T1, T2, T3, T4, T5, T6, T7, TR>> @this, Validation<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Validation<Func<T2, T3, T4, T5, T6, T7, T8, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>
        (this Validation<Func<T1, T2, T3, T4, T5, T6, T7, T8, TR>> @this, Validation<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Validation<Func<T2, T3, T4, T5, T6, T7, T8, T9, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>
        (this Validation<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>> @this, Validation<T1> arg)
        => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

    public static Validation<TRr> Map<TR, TRr>
        (this Validation<TR> @this, Func<TR, TRr> f)
        => @this.Match(
            invalid: Invalid<TRr>,
            valid: t => Valid(f(t)));

    public static Validation<Func<T2, TR>> Map<T1, T2, TR>(this Validation<T1> @this
        , Func<T1, T2, TR> func)
        => @this.Map(func.Curry());

    public static Validation<Unit> ForEach<TR>
        (this Validation<TR> @this, Action<TR> act)
        => Map(@this, act.ToFunc());

    public static Validation<T> Do<T>
        (this Validation<T> @this, Action<T> action)
    {
        @this.ForEach(action);
        return @this;
    }

    public static Validation<TR> Bind<T, TR>
        (this Validation<T> val, Func<T, Validation<TR>> f)
        => val.Match(
            invalid: Invalid<TR>,
            valid: f);

    // LINQ

    public static Validation<TR> Select<T, TR>(this Validation<T> @this
        , Func<T, TR> map) => @this.Map(map);

    public static Validation<TRr> SelectMany<T, TR, TRr>(this Validation<T> @this
        , Func<T, Validation<TR>> bind, Func<T, TR, TRr> project)
        => @this.Match(
            invalid: Invalid<TRr>,
            valid: t => bind(t).Match(
                invalid: Invalid<TRr>,
                valid: r => Valid(project(t, r))));
}