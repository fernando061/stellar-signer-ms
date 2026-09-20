using System.Security.Cryptography;
namespace StellarSigner.Application.Abstractions.Cryptography;
public sealed class MasterKeyMaterial(byte[] key, byte[] chainCode) : IDisposable
{
    public byte[] Key { get; } = key;
    public byte[] ChainCode { get; } = chainCode;
    public void Dispose() { CryptographicOperations.ZeroMemory(Key); CryptographicOperations.ZeroMemory(ChainCode); }
}
public interface IMasterKeyProvider { MasterKeyMaterial Load(); bool IsAvailable(); }
