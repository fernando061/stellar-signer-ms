using System.Security.Cryptography;
using StellarSigner.Application.Abstractions.Cryptography;
namespace StellarSigner.Infrastructure.DrivenAdapter.MasterKey;
public sealed class AesGcmSecretProtector : ISecretProtector, IDisposable
{
    private readonly byte[] _key;
    public AesGcmSecretProtector(string encodedKey)
    {
        if (string.IsNullOrWhiteSpace(encodedKey)) throw new InvalidOperationException("MasterKey:WrapKey is required");
        _key = Convert.FromBase64String(encodedKey);
        if (_key.Length != 32) throw new InvalidOperationException("MasterKey:WrapKey must be 32 bytes in base64");
    }
    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        var output = new byte[1 + 12 + 16 + plaintext.Length];
        output[0] = 1;
        RandomNumberGenerator.Fill(output.AsSpan(1, 12));
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(output.AsSpan(1, 12), plaintext, output.AsSpan(29), output.AsSpan(13, 16), output.AsSpan(0, 1));
        return output;
    }
    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length < 30 || ciphertext[0] != 1) throw new CryptographicException("Invalid master payload format");
        var output = new byte[ciphertext.Length - 29];
        using var aes = new AesGcm(_key, 16);
        try { aes.Decrypt(ciphertext.Slice(1, 12), ciphertext.Slice(29), ciphertext.Slice(13, 16), output, ciphertext.Slice(0, 1)); }
        catch { CryptographicOperations.ZeroMemory(output); throw; }
        return output;
    }
    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}
