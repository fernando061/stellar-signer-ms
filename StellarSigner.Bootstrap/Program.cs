using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using NBitcoin;
using StellarSigner.Infrastructure.DrivenAdapter.MasterKey;

var workingDirectory = Directory.GetCurrentDirectory();
var configurationPath = new[]
{
    Path.Combine(workingDirectory, "StellarSigner.Api", "appsettings.Local.json"),
    Path.Combine(workingDirectory, "appsettings.Local.json")
}.FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("StellarSigner.Api/appsettings.Local.json is required");
using var configuration = JsonDocument.Parse(File.ReadAllText(configurationPath));
var root = configuration.RootElement;
var masterKey = root.TryGetProperty("MasterKey", out var configuredMasterKey)
    ? configuredMasterKey
    : throw new InvalidOperationException("MasterKey configuration is required");
var wrapKey = masterKey.TryGetProperty("WrapKey", out var configuredWrapKey)
    ? configuredWrapKey.GetString()
    : null;
var configuredPath = masterKey.TryGetProperty("FilePath", out var configuredFilePath)
    ? configuredFilePath.GetString()
    : null;
if (string.IsNullOrWhiteSpace(wrapKey) || string.IsNullOrWhiteSpace(configuredPath))
    throw new InvalidOperationException("MasterKey:WrapKey and MasterKey:FilePath are required");
var configurationDirectory = Path.GetDirectoryName(Path.GetFullPath(configurationPath))!;
var path = Path.IsPathRooted(configuredPath)
    ? configuredPath
    : Path.GetFullPath(Path.Combine(configurationDirectory, configuredPath));
if (File.Exists(path)) throw new InvalidOperationException("Master payload already exists; refusing to overwrite");
using var protector = new AesGcmSecretProtector(wrapKey);
var mnemonic = new Mnemonic(Wordlist.English, WordCount.TwentyFour);
var seed = mnemonic.DeriveSeed();
var master = HMACSHA512.HashData(Encoding.ASCII.GetBytes("ed25519 seed"), seed);
try
{
    Console.WriteLine("Back up these 24 words offline. They will not be shown again:");
    Console.WriteLine(mnemonic.ToString());
    Console.Write("Type BACKED_UP to confirm the offline backup: ");
    if (Console.ReadLine() != "BACKED_UP") throw new OperationCanceledException("Backup not confirmed");
    var payload = new byte[65];
    payload[0] = 1;
    master.CopyTo(payload.AsSpan(1));
    try
    {
        var encrypted = protector.Protect(payload);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(encrypted);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        if (OperatingSystem.IsWindows())
        {
            var sid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Current Windows user unavailable");
            var acl = new FileSecurity();
            acl.SetOwner(sid);
            acl.SetAccessRuleProtection(true, false);
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Read | FileSystemRights.Write,
                AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(acl);
        }
        CryptographicOperations.ZeroMemory(encrypted);
        Console.WriteLine("Encrypted master payload created.");
    }
    finally { CryptographicOperations.ZeroMemory(payload); }
}
finally { CryptographicOperations.ZeroMemory(seed); CryptographicOperations.ZeroMemory(master); }
