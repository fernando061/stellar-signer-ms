using System.Security.Cryptography;
using System.Text;
using StellarSigner.Application.Abstractions.Cryptography;
using StellarSigner.Domain.ValueObjects;
using StellarSigner.Infrastructure.DrivenAdapter.Cryptography;
namespace StellarSigner.UnitTests;
public sealed class WalletDerivationTests
{
    private sealed class VectorMaster : IMasterKeyProvider
    {
        public MasterKeyMaterial Load()
        {
            var seed = Convert.FromHexString("e4a5a632e70943ae7f07659df1332160937fad82587216a4c64315a0fb39497ee4a01f76ddab4cba68147977f3a147b6ad584c41808e8238a07f6cc4b582f186");
            var digest = HMACSHA512.HashData(Encoding.ASCII.GetBytes("ed25519 seed"), seed);
            CryptographicOperations.ZeroMemory(seed);
            var result = new MasterKeyMaterial(digest[..32], digest[32..]);
            CryptographicOperations.ZeroMemory(digest);
            return result;
        }
        public bool IsAvailable() => true;
    }
    [Fact]
    public void MatchesOfficialSep5Vectors()
    {
        var service = new Slip10KeyDerivationService(new VectorMaster());
        using var zero = service.Derive(0);
        using var one = service.Derive(1);
        using var again = service.Derive(0);
        Assert.Equal("GDRXE2BQUC3AZNPVFSCEZ76NJ3WWL25FYFK6RGZGIEKWE4SOOHSUJUJ6", zero.PublicKey);
        Assert.Equal("GBAW5XGWORWVFE2XTJYDTLDHXTY2Q2MO73HYCGB3XMFMQ562Q2W2GJQX", one.PublicKey);
        Assert.Equal(zero.PublicKey, again.PublicKey);
        Assert.NotEqual(zero.PublicKey, one.PublicKey);
        Assert.Equal("m/44'/148'/0'", new DerivationPath(0).Value);
    }
}
