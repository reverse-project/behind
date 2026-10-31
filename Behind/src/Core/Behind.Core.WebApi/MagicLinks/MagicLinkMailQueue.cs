using System.Threading.Channels;
using Behind.Core.WebApi.Identity;

namespace Behind.Core.WebApi.MagicLinks;

public sealed record MagicLinkMail(ApplicationUser User, string Email, string Link);

public interface IMagicLinkMailQueue
{
    ChannelReader<MagicLinkMail> Reader { get; }

    bool TryEnqueue(MagicLinkMail mail);
}

public sealed class MagicLinkMailQueue : IMagicLinkMailQueue
{
    public const int Capacity = 1000;

    private readonly Channel<MagicLinkMail> _channel = Channel.CreateBounded<MagicLinkMail>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    public ChannelReader<MagicLinkMail> Reader => _channel.Reader;

    public bool TryEnqueue(MagicLinkMail mail) => _channel.Writer.TryWrite(mail);
}
