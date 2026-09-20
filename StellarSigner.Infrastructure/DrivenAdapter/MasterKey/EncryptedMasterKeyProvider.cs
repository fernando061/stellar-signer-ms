using System.Security.Cryptography;
using StellarSigner.Application.Abstractions.Cryptography;
namespace StellarSigner.Infrastructure.DrivenAdapter.MasterKey;
public sealed class EncryptedMasterKeyProvider(ISecretProtector protector) : IMasterKeyProvider
{
    public MasterKeyMaterial Load()
    {
        var path = Environment.GetEnvironmentVariable("SIGNER_MASTER_KEY_FILE") ?? throw new InvalidOperationException("SIGNER_MASTER_KEY_FILE is required");
        var encrypted = File.ReadAllBytes(path);
        try
        {
            var plaintext = protector.Unprotect(encrypted);
            try
            {
                if (plaintext.Length != 65 || plaintext[0] != 1) throw new CryptographicException("Invalid master payload format");
                return new MasterKeyMaterial(plaintext.AsSpan(1, 32).ToArray(), plaintext.AsSpan(33, 32).ToArray());
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        finally { CryptographicOperations.ZeroMemory(encrypted); }
    }
    public bool IsAvailable()
    {
        try { using var material = Load(); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or InvalidOperationException) { return false; }
    }
}
