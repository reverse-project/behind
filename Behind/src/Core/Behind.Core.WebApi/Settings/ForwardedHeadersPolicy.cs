using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Behind.Core.WebApi.Settings;

public sealed class ForwardedHeadersPolicy
{
    private readonly IPNetwork[] _networks;
    private readonly int _forwardLimit;

    private ForwardedHeadersPolicy(IPNetwork[] networks, int forwardLimit)
    {
        _networks = networks;
        _forwardLimit = forwardLimit;
    }

    private static readonly ForwardedHeadersPolicy Disabled = new([], 0);

    public static ForwardedHeadersPolicy Read(IConfiguration configuration)
    {
        try
        {
            var settings = configuration.GetSection(ForwardedHeadersConfiguration.SectionName)
                .Get<ForwardedHeadersConfiguration>();
            return settings is not null && TryCreate(settings, out var policy) ? policy : Disabled;
        }
        catch (InvalidOperationException)
        {
            return Disabled;
        }
    }

    public static bool TryCreate(ForwardedHeadersConfiguration configuration,
        [NotNullWhen(true)] out ForwardedHeadersPolicy? policy)
    {
        policy = null;
        if (configuration.ForwardLimit < 1 || configuration.KnownForwarders is null ||
            (configuration.Enabled && configuration.KnownForwarders.Length == 0))
        {
            return false;
        }

        var networks = new List<IPNetwork>();
        foreach (var entry in configuration.KnownForwarders)
        {
            if (!TryNetwork(entry, out var network))
            {
                return false;
            }
            networks.Add(network);
        }

        policy = new ForwardedHeadersPolicy(configuration.Enabled ? networks.ToArray() : [], configuration.ForwardLimit);
        return true;
    }

    private static bool TryNetwork(string? entry, out IPNetwork network)
    {
        network = default;
        var parts = entry?.Trim().Split('/');
        if (parts is null || parts.Length > 2 || !TryAddress(parts[0], out var address))
        {
            return false;
        }

        var width = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        var prefix = width;
        if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.None,
                CultureInfo.InvariantCulture, out prefix) || prefix > width))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
            prefix -= 96;
            width = 32;
        }

        if (prefix < (width == 32 ? 24 : 64) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        for (var bit = prefix; bit < width; bit++)
        {
            if ((bytes[bit / 8] & (128 >> (bit % 8))) != 0)
            {
                return false;
            }
        }

        network = new IPNetwork(address, prefix);
        return true;
    }

    private static bool TryAddress(ReadOnlySpan<char> value, [NotNullWhen(true)] out IPAddress? address)
    {
        address = null;
        if (value.ContainsAny('%', '[', ']') || !IPAddress.TryParse(value, out address))
        {
            return false;
        }
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return value.SequenceEqual(address.ToString());
        }
        if (value.Contains('.'))
        {
            var suffix = value[(value.LastIndexOf(':') + 1)..];
            return IPAddress.TryParse(suffix, out var v4) && suffix.SequenceEqual(v4.ToString());
        }
        return true;
    }

    internal IPAddress? Resolve(IPAddress? peer, StringValues forwardedFor)
    {
        var current = peer?.IsIPv4MappedToIPv6 == true ? peer.MapToIPv4() : peer;
        IPAddress? resolved = null;
        var remaining = _forwardLimit;
        for (var line = forwardedFor.Count - 1; line >= 0; line--)
        {
            var values = forwardedFor[line].AsSpan();
            while (remaining > 0 && current is not null && _networks.Any(network => network.Contains(current)))
            {
                var comma = values.LastIndexOf(',');
                if (!TryAddress(values[(comma + 1)..].Trim(), out var candidate))
                {
                    return resolved;
                }
                current = candidate.IsIPv4MappedToIPv6 ? candidate.MapToIPv4() : candidate;
                resolved = current;
                remaining--;
                if (comma < 0)
                {
                    break;
                }
                values = values[..comma];
            }
        }
        return resolved;
    }
}
