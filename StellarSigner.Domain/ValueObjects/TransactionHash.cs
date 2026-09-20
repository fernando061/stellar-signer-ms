namespace StellarSigner.Domain.ValueObjects;
public readonly record struct TransactionHash
{
    public string Value { get; }
    public TransactionHash(string value)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid hash", nameof(value));
        Value = value.ToLowerInvariant();
    }
    public override string ToString() => Value;
}
