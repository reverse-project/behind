using System.Collections.Concurrent;
using System.Security.Cryptography;
using Behind.Common;

namespace Behind.Core.WebApi.Captcha;

public sealed class CaptchaService : ICaptchaService
{
    public const int CodeLength = 5;
    public const int KeySize = 32;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string ImagePrefix = "data:" + CaptchaImageExtensions.ContentType + ";base64,";

    private readonly TimeProvider _timeProvider;
    private readonly byte[] _key;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _consumed = new();

    public CaptchaService(TimeProvider timeProvider) : this(timeProvider, RandomNumberGenerator.GetBytes(KeySize))
    {
    }

    public CaptchaService(TimeProvider timeProvider, byte[] key)
    {
        _timeProvider = timeProvider;
        _key = key;
    }

    public CaptchaChallenge Create()
    {
        var code = RandomNumberGenerator.GetString(CaptchaGlyphs.Alphabet, CodeLength);
        var ticket = new CaptchaTicket(_timeProvider.GetUtcNow().Add(Lifetime), code);

        return new CaptchaChallenge(ticket.Seal(_key), ImagePrefix + Convert.ToBase64String(code.ToCaptchaImage()));
    }

    public bool Verify(CaptchaResponse? response)
    {
        if (response is null)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();

        Forget(now);

        return CaptchaTicket.Open(response.Id, _key).Match(
            () => false,
            ticket => ticket.Expires > now
                      && _consumed.TryAdd(response.Id, ticket.Expires)
                      && ticket.Accepts(response.Answer));
    }

    private void Forget(DateTimeOffset now)
    {
        foreach (var (id, expires) in _consumed)
        {
            if (expires <= now)
            {
                _consumed.TryRemove(id, out _);
            }
        }
    }
}
