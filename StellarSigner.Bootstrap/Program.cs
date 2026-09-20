using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using NBitcoin;
using StellarSigner.Infrastructure.DrivenAdapter.MasterKey;

var path = Environment.GetEnvironmentVariable("SIGNER_MASTER_KEY_FILE") ?? throw new InvalidOperationException("SIGNER_MASTER_KEY_FILE is required");
if (File.Exists(path)) throw new InvalidOperationException("Master payload already exists; refusing to overwrite");
using var protector = new AesGcmSecretProtector();
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
