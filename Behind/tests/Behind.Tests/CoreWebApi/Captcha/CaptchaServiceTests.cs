using System.Security.Cryptography;
using Behind.Core.WebApi.Captcha;

namespace Behind.Tests.CoreWebApi.Captcha;

public class CaptchaServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly byte[] _key = RandomNumberGenerator.GetBytes(CaptchaService.KeySize);
    private readonly MovableTimeProvider _time = new(Start);
    private readonly CaptchaService _service;

    public CaptchaServiceTests()
    {
        _service = new CaptchaService(_time, _key);
    }

    private string AnswerOf(CaptchaChallenge challenge) =>
        CaptchaTicket.Open(challenge.Id, _key).Match(
            () => throw new InvalidOperationException(),
            ticket => ticket.Answer);

    [Fact]
    public void Create_ReturnsAPngDataUriAndAnOpaqueId()
    {
        var challenge = _service.Create();

        Assert.StartsWith("data:image/png;base64,", challenge.Image);
        Assert.DoesNotContain(AnswerOf(challenge), challenge.Id);
        Assert.Equal(CaptchaService.CodeLength, AnswerOf(challenge).Length);
        Assert.All(AnswerOf(challenge), character => Assert.Contains(character, CaptchaGlyphs.Alphabet));
    }

    [Fact]
    public void Create_IssuesADifferentChallengeEveryTime()
    {
        Assert.NotEqual(_service.Create().Id, _service.Create().Id);
    }

    [Fact]
    public void Verify_AcceptsTheRightAnswer()
    {
        var challenge = _service.Create();

        Assert.True(_service.Verify(new CaptchaResponse(challenge.Id, AnswerOf(challenge))));
    }

    [Fact]
    public void Verify_IgnoresTheCaseAndSurroundingWhitespace()
    {
        var challenge = _service.Create();

        Assert.True(_service.Verify(new CaptchaResponse(challenge.Id, $"  {AnswerOf(challenge).ToLowerInvariant()} ")));
    }

    [Fact]
    public void Verify_RejectsAWrongAnswer()
    {
        var challenge = _service.Create();

        Assert.False(_service.Verify(new CaptchaResponse(challenge.Id, "nope")));
    }

    [Fact]
    public void Verify_RejectsAMissingResponse()
    {
        Assert.False(_service.Verify(null));
    }

    [Fact]
    public void Verify_RejectsAChallengeThatWasAlreadyAnswered()
    {
        var challenge = _service.Create();
        var response = new CaptchaResponse(challenge.Id, AnswerOf(challenge));

        Assert.True(_service.Verify(response));
        Assert.False(_service.Verify(response));
    }

    [Fact]
    public void Verify_UsesUpTheChallengeEvenWhenTheAnswerIsWrong()
    {
        var challenge = _service.Create();

        Assert.False(_service.Verify(new CaptchaResponse(challenge.Id, "nope")));
        Assert.False(_service.Verify(new CaptchaResponse(challenge.Id, AnswerOf(challenge))));
    }

    [Fact]
    public void Verify_RejectsAnExpiredChallenge()
    {
        var challenge = _service.Create();
        var answer = AnswerOf(challenge);

        _time.Now = Start + CaptchaService.Lifetime + TimeSpan.FromSeconds(1);

        Assert.False(_service.Verify(new CaptchaResponse(challenge.Id, answer)));
    }

    [Fact]
    public void Verify_AcceptsAChallengeUntilItExpires()
    {
        var challenge = _service.Create();
        var answer = AnswerOf(challenge);

        _time.Now = Start + CaptchaService.Lifetime - TimeSpan.FromSeconds(1);

        Assert.True(_service.Verify(new CaptchaResponse(challenge.Id, answer)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a token")]
    [InlineData("AAAA")]
    public void Verify_RejectsAnIdThatIsNotAChallenge(string id)
    {
        Assert.False(_service.Verify(new CaptchaResponse(id, "ABCDE")));
    }

    [Fact]
    public void Verify_RejectsATamperedId()
    {
        var challenge = _service.Create();
        var answer = AnswerOf(challenge);
        var flipped = challenge.Id[^2] == 'A' ? 'B' : 'A';
        var tampered = challenge.Id[..^2] + flipped + challenge.Id[^1];

        Assert.False(_service.Verify(new CaptchaResponse(tampered, answer)));
    }

    [Fact]
    public void Verify_RejectsAChallengeIssuedByAnotherInstance()
    {
        var other = new CaptchaService(_time, RandomNumberGenerator.GetBytes(CaptchaService.KeySize));
        var challenge = other.Create();

        Assert.False(_service.Verify(new CaptchaResponse(challenge.Id, "ABCDE")));
    }

    [Fact]
    public async Task Verify_AcceptsAChallengeOnlyOnceUnderConcurrency()
    {
        var challenge = _service.Create();
        var response = new CaptchaResponse(challenge.Id, AnswerOf(challenge));

        var results = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(_ => Task.Run(() => _service.Verify(response))));

        Assert.Single(results, accepted => accepted);
    }

    private sealed class MovableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
