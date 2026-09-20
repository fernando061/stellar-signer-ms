namespace StellarSigner.Application.Abstractions.Cryptography;
public interface ISecretProtector { byte[] Protect(ReadOnlySpan<byte> plaintext); byte[] Unprotect(ReadOnlySpan<byte> ciphertext); }
