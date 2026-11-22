namespace Behind.Core.Domain;

public sealed record PinHash
{
    private readonly string _hash;

    public PinHash(string hash)
    {
        _hash = hash;
    }

    public static explicit operator PinHash?(string? hash) => hash is null ? null : new PinHash(hash);

    public static explicit operator string?(PinHash? hash) => hash?._hash;

    public override string ToString() => _hash;
}
