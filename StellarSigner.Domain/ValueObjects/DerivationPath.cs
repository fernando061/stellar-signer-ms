namespace StellarSigner.Domain.ValueObjects;
public readonly record struct DerivationPath
{
    public string Value { get; }
    public DerivationPath(uint index) => Value = $"m/44'/148'/{index}'";
    public override string ToString() => Value;
}
