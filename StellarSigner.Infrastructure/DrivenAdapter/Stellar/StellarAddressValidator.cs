using StellarDotnetSdk;
using StellarSigner.Application.Abstractions.Blockchain;
namespace StellarSigner.Infrastructure.DrivenAdapter.Stellar;
public sealed class StellarAddressValidator : IStellarAddressValidator
{
    public bool IsValidAccount(string address)
    {
        return !string.IsNullOrEmpty(address) && StrKey.IsValidEd25519PublicKey(address);
    }
    public bool IsValidContract(string address)
    {
        return !string.IsNullOrEmpty(address) && StrKey.IsValidContractId(address);
    }
}
