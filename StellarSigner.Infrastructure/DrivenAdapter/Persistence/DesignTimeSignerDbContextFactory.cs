using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence;

public sealed class DesignTimeSignerDbContextFactory : IDesignTimeDbContextFactory<SignerDbContext>
{
    public SignerDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__SignerDb")
            ?? "Host=localhost;Port=5432;Database=stellar_signer;Username=signer;Password=unused-design-time";
        var options = new DbContextOptionsBuilder<SignerDbContext>().UseNpgsql(connection).Options;
        return new SignerDbContext(options);
    }
}
