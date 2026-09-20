using StellarSigner.Domain.Entities;
using StellarSigner.Domain.Enums;
namespace StellarSigner.UnitTests;
public sealed class SigningRequestStateTests
{
    [Fact]
    public void SignedRequestCannotBeSignedOrRejectedAgain()
    {
        var wallet = PartnerWallet.Create(Guid.NewGuid(),
            "GDRXE2BQUC3AZNPVFSCEZ76NJ3WWL25FYFK6RGZGIEKWE4SOOHSUJUJ6", 0);
        var request = SigningRequest.Create(Guid.NewGuid(), "test-key", new string('a',64), Guid.NewGuid(), wallet,
            new string('b',64), new string('c',64), SigningOperationType.LockFunds, "USDC", wallet.PublicKey, 1m, wallet.PublicKey);
        request.MarkSigned("signed-xdr", new string('d',64));
        Assert.Equal(SigningStatus.Signed, request.Status);
        Assert.Throws<InvalidOperationException>(() => request.MarkSigned("another-xdr", new string('e',64)));
        Assert.Throws<InvalidOperationException>(() => request.Reject("INVALID_TRANSACTION_XDR"));
    }
}
