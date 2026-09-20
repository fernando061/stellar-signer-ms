namespace StellarSigner.Application.Common;
public sealed class SignerException(string code, int status) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
