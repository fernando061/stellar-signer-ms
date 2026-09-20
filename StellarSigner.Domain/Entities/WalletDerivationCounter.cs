namespace StellarSigner.Domain.Entities;
public sealed class WalletDerivationCounter
{
    private WalletDerivationCounter() { }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public long NextIndex { get; private set; }
}
