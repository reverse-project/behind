namespace Behind.Core.Domain;

public sealed record TokenNonce(Guid Nonce)
{
    public static implicit operator Guid(TokenNonce tokenNonce) => tokenNonce.Nonce;
    public static implicit operator TokenNonce(Guid nonce) => new(nonce);
    public static implicit operator string(TokenNonce tokenNonce) => tokenNonce.Nonce.ToString();
    
    public override string ToString() => Nonce.ToString();
}