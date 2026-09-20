using System.Buffers.Binary;
using System.Security.Cryptography;
using StellarDotnetSdk.Accounts;
using StellarSigner.Application.Abstractions.Cryptography;
namespace StellarSigner.Infrastructure.DrivenAdapter.Cryptography;
public sealed class Slip10KeyDerivationService(IMasterKeyProvider master) : IKeyDerivationService
{
    public DerivedKey Derive(uint index)
    {
        if (index >= 0x80000000) throw new ArgumentOutOfRangeException(nameof(index));
        using var material = master.Load();
        var key = material.Key.ToArray();
        var chain = material.ChainCode.ToArray();
        Span<byte> input = stackalloc byte[37];
        try
        {
            foreach (var child in new uint[] { 44, 148, index })
            {
                input.Clear();
                key.CopyTo(input.Slice(1, 32));
                BinaryPrimitives.WriteUInt32BigEndian(input.Slice(33), child | 0x80000000);
                var digest = HMACSHA512.HashData(chain, input);
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(chain);
                key = digest.AsSpan(0, 32).ToArray();
                chain = digest.AsSpan(32, 32).ToArray();
                CryptographicOperations.ZeroMemory(digest);
                CryptographicOperations.ZeroMemory(input);
            }
            var leaf = key.ToArray();
            var pair = KeyPair.FromSecretSeed(key);
            var publicKey = pair.AccountId;
            if (pair.SeedBytes is { } pairSeed) CryptographicOperations.ZeroMemory(pairSeed);
            if (pair.PrivateKey is { } pairPrivate) CryptographicOperations.ZeroMemory(pairPrivate);
            return new DerivedKey(leaf, publicKey);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(chain); }
    }
}
