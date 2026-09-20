namespace StellarSigner.Application.Abstractions.Blockchain;
public interface IStellarAddressValidator { bool IsValidAccount(string address); bool IsValidContract(string address); }
