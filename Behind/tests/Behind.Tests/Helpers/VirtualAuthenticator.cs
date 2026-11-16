using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Behind.Tests.Helpers;

public sealed class VirtualAuthenticator : IDisposable
{
    private const byte UserPresent = 0x01;
    private const byte UserVerified = 0x04;
    private const byte AttestedCredentialData = 0x40;
    private const int Es256 = -7;

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);

    public uint Counter { get; private set; }

    public void Dispose() => _key.Dispose();

    public AuthenticatorAttestationRawResponse Register(CredentialCreateOptions options, string origin) =>
        Register(options.Challenge, options.Rp.Id, origin);

    public AuthenticatorAttestationRawResponse Register(byte[] challenge, string rpId, string origin)
    {
        var clientData = ClientData("webauthn.create", challenge, origin);
        var authenticatorData = AuthenticatorData(rpId, UserPresent | UserVerified | AttestedCredentialData, true);

        return new AuthenticatorAttestationRawResponse
        {
            Id = Base64Url.EncodeToString(CredentialId),
            RawId = CredentialId,
            Type = PublicKeyCredentialType.PublicKey,
            Response = new AuthenticatorAttestationRawResponse.AttestationResponse
            {
                AttestationObject = AttestationObject(authenticatorData),
                ClientDataJson = clientData
            }
        };
    }

    public AuthenticatorAssertionRawResponse Assert(
        AssertionOptions options,
        string origin,
        byte[]? userHandle = null,
        byte[]? claimedCredentialId = null)
    {
        var credentialId = claimedCredentialId ?? CredentialId;
        Counter++;

        var clientData = ClientData("webauthn.get", options.Challenge, origin);
        var authenticatorData = AuthenticatorData(options.RpId!, UserPresent | UserVerified, false);
        var signature = _key.SignData(
            [.. authenticatorData, .. SHA256.HashData(clientData)],
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return new AuthenticatorAssertionRawResponse
        {
            Id = Base64Url.EncodeToString(credentialId),
            RawId = credentialId,
            Type = PublicKeyCredentialType.PublicKey,
            Response = new AuthenticatorAssertionRawResponse.AssertionResponse
            {
                AuthenticatorData = authenticatorData,
                Signature = signature,
                ClientDataJson = clientData,
                UserHandle = userHandle
            }
        };
    }

    private static byte[] ClientData(string type, byte[] challenge, string origin) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["type"] = type,
            ["challenge"] = Base64Url.EncodeToString(challenge),
            ["origin"] = origin,
            ["crossOrigin"] = false
        });

    private byte[] AuthenticatorData(string rpId, byte flags, bool withCredential)
    {
        var header = new List<byte>();
        header.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)));
        header.Add(flags);
        header.AddRange(BitConverter.GetBytes(Counter).Reverse());

        if (withCredential)
        {
            header.AddRange(new byte[16]);
            header.AddRange(BitConverter.GetBytes((ushort)CredentialId.Length).Reverse());
            header.AddRange(CredentialId);
            header.AddRange(CoseKey());
        }

        return [.. header];
    }

    private byte[] CoseKey()
    {
        var parameters = _key.ExportParameters(false);
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

        writer.WriteStartMap(5);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteInt32(3);
        writer.WriteInt32(Es256);
        writer.WriteInt32(-1);
        writer.WriteInt32(1);
        writer.WriteInt32(-2);
        writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();

        return writer.Encode();
    }

    private static byte[] AttestationObject(byte[] authenticatorData)
    {
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString("none");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(authenticatorData);
        writer.WriteEndMap();

        return writer.Encode();
    }
}
