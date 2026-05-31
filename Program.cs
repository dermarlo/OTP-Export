using System.Text.Json;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;

// Parse arguments: --input <path>  and/or  --output <path>
string? inputArg = null;
string? outputArg = null;

for (int i = 0; i < args.Length; i++)
{
    if ((args[i] == "--input" || args[i] == "-i") && i + 1 < args.Length)
        inputArg = args[++i];
    else if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
        outputArg = args[++i];
    else
    {
        Console.Error.WriteLine($"Unknown argument: {args[i]}");
        Console.Error.WriteLine("Usage: OTP-Export [--input <Accounts.json>] [--output <accounts-plain.json>]");
        return 1;
    }
}

string accountsPath;

if (inputArg != null)
{
    accountsPath = Path.GetFullPath(inputArg);
    if (!File.Exists(accountsPath))
    {
        Console.Error.WriteLine("Input file not found: " + accountsPath);
        return 1;
    }
}
else
{
    // Auto-discover the WinOTP Authenticator package directory
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var packagesDir = Path.Combine(localAppData, "Packages");

    if (!Directory.Exists(packagesDir))
    {
        Console.Error.WriteLine($"Packages directory not found: {packagesDir}");
        return 1;
    }

    var packageDir = Directory.GetDirectories(packagesDir)
        .FirstOrDefault(d => Path.GetFileName(d).Contains("Authenticator", StringComparison.OrdinalIgnoreCase));

    if (packageDir == null)
    {
        Console.Error.WriteLine("Could not find an Authenticator package directory.");
        Console.Error.WriteLine("Directories searched in: " + packagesDir);
        Console.Error.WriteLine("\nAvailable package directories (first 30):");
        foreach (var dir in Directory.GetDirectories(packagesDir).Take(30))
            Console.Error.WriteLine("  " + Path.GetFileName(dir));
        Console.Error.WriteLine("\nIf yours is listed above, re-run with --input pointing directly to the Accounts.json file.");
        return 1;
    }

    Console.WriteLine("Found package directory: " + Path.GetFileName(packageDir));
    accountsPath = Path.Combine(packageDir, "LocalState", "Accounts.json");

    if (!File.Exists(accountsPath))
    {
        Console.Error.WriteLine("Accounts.json not found in: " + Path.Combine(packageDir, "LocalState"));
        Console.Error.WriteLine("The matched package may not be WinOTP Authenticator, or the app has no saved accounts.");
        Console.Error.WriteLine("If you have a backup of Accounts.json, re-run with: --input \"<path to Accounts.json>\"");
        return 1;
    }
}

if (!File.Exists(accountsPath))
{
    Console.Error.WriteLine("Input file not found: " + accountsPath);
    return 1;
}

var fileInfo = new FileInfo(accountsPath);
if (fileInfo.Length == 0)
{
    Console.Error.WriteLine("Accounts.json exists but is empty. No accounts were saved.");
    return 1;
}

Console.WriteLine("Reading: " + accountsPath);
Console.WriteLine("File size: " + fileInfo.Length + " bytes");

byte[] bytes = File.ReadAllBytes(accountsPath);
var buffer = CryptographicBuffer.CreateFromByteArray(bytes);

string json;
try
{
    // Must run as the same Windows user who encrypted the data (DPAPI LOCAL=user scope)
    var provider = new DataProtectionProvider();
    var decryptedBuffer = await provider.UnprotectAsync(buffer);
    json = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decryptedBuffer);
}
catch (Exception ex)
{
    Console.Error.WriteLine("\nDecryption failed: " + ex.Message);
    Console.Error.WriteLine("Ensure you are running this tool as the same Windows user who used the Authenticator app.");
    return 1;
}

// Pretty-print the JSON
string pretty;
try
{
    using var doc = JsonDocument.Parse(json);
    pretty = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
}
catch
{
    pretty = json; // fall back to raw if parse fails
}

Console.WriteLine("\n=== Decrypted accounts ===");
Console.WriteLine(pretty);

var outputPath = outputArg != null
    ? Path.GetFullPath(outputArg)
    : Path.Combine(Environment.CurrentDirectory, "accounts-plain.json");

File.WriteAllText(outputPath, pretty);
Console.WriteLine("\nSaved to: " + outputPath);
Console.WriteLine("\nEach entry contains:");
Console.WriteLine("  Username: the account login / email");
Console.WriteLine("  Secret: the Base32 TOTP seed (this is what you need to re-add accounts)");
Console.WriteLine("  Service: the service name (Google, GitHub, etc.)");
Console.WriteLine("\nTo re-add an account in any 2FA app, use the Secret value.");
Console.WriteLine("For the Authenticator app on Win11, use:");
Console.WriteLine("  otpauth://totp/<Service>:<Username>?secret=<Secret>&issuer=<Service>");

return 0;
