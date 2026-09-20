namespace StellarSigner.Domain.ValueObjects;
public readonly record struct StellarPublicKey
{
    public string Value { get; }
    public StellarPublicKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 56 || value[0] != 'G')
            throw new ArgumentException("Invalid Stellar public key", nameof(value));
        Value = value;
    }
    public override string ToString() => Value;
}
