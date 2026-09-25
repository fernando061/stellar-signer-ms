using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence;

public sealed class DesignTimeSignerDbContextFactory : IDesignTimeDbContextFactory<SignerDbContext>
{
    public SignerDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__SignerDb")
            ?? ReadLocalConnectionString()
            ?? "Host=localhost;Port=5432;Database=stellar_signer;Username=signer;Password=unused-design-time";
        var options = new DbContextOptionsBuilder<SignerDbContext>().UseNpgsql(connection).Options;
        return new SignerDbContext(options);
    }

    private static string? ReadLocalConnectionString()
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(workingDirectory, "StellarSigner.Api", "appsettings.Local.json"),
            Path.Combine(workingDirectory, "appsettings.Local.json")
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null) return null;

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
            && connectionStrings.TryGetProperty("SignerDb", out var signerDb)
                ? signerDb.GetString()
                : null;
    }
}
