namespace StellarSigner.Application.Abstractions.Cryptography;
public interface ITransactionSigner { string Sign(string unsignedXdr, byte[] privateSeed, string networkPassphrase); }
