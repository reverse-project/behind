using System.ComponentModel;
using System.Text.Json.Serialization;
using Behind.Common.Converters;
using Unit = System.ValueTuple;
using Behind.Common.Option;

namespace Behind.Common
{
    using static FunctionalExtensions;

    public static partial class FunctionalExtensions
    {
        public static Option<T> Some<T>(T value) => new Some<T>(value); // wrap the given value into a Some
        public static None None => None.Default; // the None value
    }

    [JsonConverter(typeof(OptionJsonConverterFactory))]
    public readonly struct Option<T> : IEquatable<None>, IEquatable<Option<T>>
    {
        public override bool Equals(object? obj)
        {
            return obj is Option<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_value, _isSome);
        }

        readonly T _value;
        readonly bool _isSome;
        bool IsNone => !_isSome;

        private Option(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException();
            }

            _isSome = true;
            _value = value;
        }

        public static implicit operator Option<T>(None _) => new();
        public static implicit operator Option<T>(Some<T> some) => new(some.Value);

        public static implicit operator Option<T>(T value) => value == null ? None : Some(value);

        public TR Match<TR>(Func<TR> none, Func<T, TR> some)
            => _isSome ? some(_value) : none();

        public IEnumerable<T> AsEnumerable()
        {
            if (_isSome)
            {
                yield return _value;
            }
        }

        public bool Equals(Option<T> other)
            => _isSome == other._isSome
               && (IsNone || _value?.Equals(other._value) == true);

        public bool Equals(None _) => IsNone;

        public static bool operator ==(Option<T> @this, Option<T> other) => @this.Equals(other);
        public static bool operator !=(Option<T> @this, Option<T> other) => !(@this == other);

        public override string ToString() => _isSome ? $"Some({_value})" : "None";
    }


    namespace Option
    {
        public record struct None
        {
            internal static readonly None Default = new None();
        }

        public struct Some<T>
        {
            internal T Value { get; }

            internal Some(T value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value)
                        , "Cannot wrap a null value in a 'Some'; use 'None' instead");
                }

                Value = value;
            }
        }
    }

    public static class OptionExtensions
    {
        public static Option<TR> Apply<T, TR>
            (this Option<Func<T, TR>> @this, Option<T> arg)
            => @this.Match(
                () => None,
                (func) => arg.Match(
                    () => None,
                    (val) => Some(func(val))));

        public static Option<Func<T2, TR>> Apply<T1, T2, TR>
            (this Option<Func<T1, T2, TR>> @this, Option<T1> arg)
            => Apply(
                Map(@this, FunctionalExtensions.Curry), arg);

        public static Option<Func<T2, T3, TR>> Apply<T1, T2, T3, TR>
            (this Option<Func<T1, T2, T3, TR>> @this, Option<T1> arg)
            => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

        public static Option<Func<T2, T3, T4, TR>> Apply<T1, T2, T3, T4, TR>
            (this Option<Func<T1, T2, T3, T4, TR>> @this, Option<T1> arg)
            => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

        public static Option<Func<T2, T3, T4, T5, TR>> Apply<T1, T2, T3, T4, T5, TR>
            (this Option<Func<T1, T2, T3, T4, T5, TR>> @this, Option<T1> arg)
            => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

        public static Option<Func<T2, T3, T4, T5, T6, TR>> Apply<T1, T2, T3, T4, T5, T6, TR>
            (this Option<Func<T1, T2, T3, T4, T5, T6, TR>> @this, Option<T1> arg)
            => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

        public static Option<Func<T2, T3, T4, T5, T6, T7, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, TR>
            (this Option<Func<T1, T2, T3, T4, T5, T6, T7, TR>> @this, Option<T1> arg)
            => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

        public static Option<Func<T2, T3, T4, T5, T6, T7, T8, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, T8, TR>
            (this Option<Func<T1, T2, T3, T4, T5, T6, T7, T8, TR>> @this, Option<T1> arg)
            => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

        public static Option<Func<T2, T3, T4, T5, T6, T7, T8, T9, TR>> Apply<T1, T2, T3, T4, T5, T6, T7, T8,
                T9, TR>
            (this Option<Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TR>> @this, Option<T1> arg)
            => Apply(@this.Map(FunctionalExtensions.CurryFirst), arg);

        public static Option<TR> Bind<T, TR>
            (this Option<T> optT, Func<T, Option<TR>> f)
            => optT.Match(
                () => None,
                (t) => f(t));

        public static IEnumerable<TR> Bind<T, TR>
            (this Option<T> @this, Func<T, IEnumerable<TR>> func)
            => @this.AsEnumerable().Bind(func);

        public static Option<Unit> ForEach<T>(this Option<T> @this, Action<T> action)
            => Map(@this, action.ToFunc());

        public static Option<TR> Map<T, TR>
            (this None _, Func<T, TR> f)
            => None;

        public static Option<TR> Map<T, TR>
            (this Some<T> some, Func<T, TR> f)
            => Some(f(some.Value));

        public static Option<TR> Map<T, TR>
            (this Option<T> optT, Func<T, TR> f)
            => optT.Match(
                () => None,
                (t) => Some(f(t)));

        public static Option<Func<T2, TR>> Map<T1, T2, TR>
            (this Option<T1> @this, Func<T1, T2, TR> func)
            => @this.Map(FunctionalExtensions.Curry(func));

        public static Option<Func<T2, T3, TR>> Map<T1, T2, T3, TR>
            (this Option<T1> @this, Func<T1, T2, T3, TR> func)
            => @this.Map(FunctionalExtensions.CurryFirst(func));

        public static IEnumerable<Option<TR>> Traverse<T, TR>(this Option<T> @this
            , Func<T, IEnumerable<TR>> func)
            => @this.Match(
                () => List((Option<TR>)None),
                (t) => func(t).Map(r => Some(r)));

        // utilities

        public static Unit Match<T>(this Option<T> @this, Action none, Action<T> some)
            => @this.Match(ActionExtensions.ToFunc(none), ActionExtensions.ToFunc(some));

        internal static bool IsSome<T>(this Option<T> @this)
            => @this.Match(
                () => false,
                (_) => true);

        internal static T ValueUnsafe<T>(this Option<T> @this)
            => @this.Match(
                () => { throw new InvalidOperationException(); },
                (t) => t);

        public static T GetOrElse<T>(this Option<T> opt, T defaultValue)
            => opt.Match(
                () => defaultValue,
                (t) => t);

        public static T GetOrElse<T>(this Option<T> opt, Func<T> fallback)
            => opt.Match(
                () => fallback(),
                (t) => t);

        public static Task<T> GetOrElse<T>(this Option<T> opt, Func<Task<T>> fallback)
            => opt.Match(
                () => fallback(),
                (t) => Async(t));

        public static Option<T> OrElse<T>(this Option<T> left, Option<T> right)
            => left.Match(
                () => right,
                (_) => left);

        public static Option<T> OrElse<T>(this Option<T> left,
            Func<Option<T>> right)
            => left.Match(
                () => right(),
                (_) => left);


        public static Validation<T> ToValidation<T>(this Option<T> opt, Func<Error> error)
            => opt.Match(
                () => Invalid(error()),
                (t) => Valid(t));

        // LINQ

        public static Option<TR> Select<T, TR>(this Option<T> @this, Func<T, TR> func)
            => @this.Map(func);

        public static Option<T> Where<T>
            (this Option<T> optT, Func<T, bool> predicate)
            => optT.Match(
                () => None,
                (t) => predicate(t) ? optT : None);

        public static Option<TRr> SelectMany<T, TR, TRr>
            (this Option<T> opt, Func<T, Option<TR>> bind, Func<T, TR, TRr> project)
            => opt.Match(
                () => None,
                (t) => bind(t).Match(
                    () => None,
                    (r) => Some(project(t, r))));
    }
}