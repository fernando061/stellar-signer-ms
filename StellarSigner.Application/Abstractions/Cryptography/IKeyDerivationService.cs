using System.Security.Cryptography;
namespace StellarSigner.Application.Abstractions.Cryptography;
public sealed class DerivedKey(byte[] seed, string publicKey) : IDisposable
{
    public byte[] Seed { get; } = seed;
    public string PublicKey { get; } = publicKey;
    public void Dispose() => CryptographicOperations.ZeroMemory(Seed);
}
public interface IKeyDerivationService { DerivedKey Derive(uint index); }
