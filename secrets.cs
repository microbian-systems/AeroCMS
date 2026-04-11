#r "nuget: Microsoft.AspNetCore.DataProtection, 8.0.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 8.0.0"

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;


// --- CONFIGURATION ---
const string pfxPath = "dpapi.pfx";
const string pfxPassword = "YourSecurePassword123!"; // Use the same password from your cert
const string appName = "AeroCMS-Vault";             // Must match your main app's Purpose string

Console.WriteLine("--- .NET Data Protection Encryption Utility ---");
Console.Write("Enter the raw secret to encrypt: ");
string rawSecret = Console.ReadLine() ?? "";

if (string.IsNullOrWhiteSpace(rawSecret)) return;

try
{
    // 1. Load the same certificate your production app uses
    var cert = new X509Certificate2(pfxPath, pfxPassword, X509KeyStorageFlags.Exportable);

    // 2. Setup a local Data Protection provider
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddDataProtection()
        .SetApplicationName(appName)
        .ProtectKeysWithCertificate(cert)
        // Store temporary keys in a local folder
        .PersistKeysToFileSystem(new DirectoryInfo("./temp-keys"));

    var services = serviceCollection.BuildServiceProvider();
    var protector = services.GetDataProtector("VaultBootstrapper");

    // 3. Encrypt the secret
    string protectedPayload = protector.Protect(rawSecret);

    Console.WriteLine("\n--- ENCRYPTED TOKEN ---");
    Console.WriteLine("Copy and paste this into your appsettings.json:");
    Console.WriteLine(protectedPayload);
    Console.WriteLine("-----------------------\n");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}