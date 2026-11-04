using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Behind.Common.Configuration;

public sealed class ReloadableOption<T> : IOptions<T>, IOptionsSnapshot<T>, IOptionsMonitor<T>, IDisposable
    where T : class
{
    private readonly Func<IConfiguration, T> _bind;
    private readonly IConfiguration _configuration;
    private readonly Action<Exception>? _onFailure;
    private readonly IDisposable _registration;
    private ImmutableList<Action<T, string?>> _listeners = [];
    private T _current;

    public ReloadableOption(IConfiguration configuration, Func<IConfiguration, T> bind, Action<Exception>? onFailure = null)
    {
        _configuration = configuration;
        _bind = bind;
        _onFailure = onFailure;
        _current = bind(configuration);
        _registration = ChangeToken.OnChange(configuration.GetReloadToken, Reload);
    }

    public T Value => Volatile.Read(ref _current);

    public T CurrentValue => Value;

    public T Get(string? name) => Value;

    public IDisposable OnChange(Action<T, string?> listener)
    {
        ImmutableInterlocked.Update(ref _listeners, listeners => listeners.Add(listener));

        return new Subscription(() => ImmutableInterlocked.Update(ref _listeners, listeners => listeners.Remove(listener)));
    }

    public void Dispose() => _registration.Dispose();

    private void Reload()
    {
        try
        {
            var bound = _bind(_configuration);
            var previous = Interlocked.Exchange(ref _current, bound);

            if (!EqualityComparer<T>.Default.Equals(previous, bound))
            {
                Volatile.Read(ref _listeners).ForEach(listener => listener(bound, null));
            }
        }
        catch (Exception exception)
        {
            _onFailure?.Invoke(exception);
        }
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
