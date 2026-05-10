using Microsoft.AspNetCore.DataProtection;

namespace Aero.Cms.Modules.Ai.Configuration;

public sealed class DataProtectionAiSecretProtector(IDataProtectionProvider dataProtectionProvider) : IAiSecretProtector
{
    private const string Prefix = "dp:";
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Aero.Cms.Modules.Ai.ProviderKeys.v1");

    public string Protect(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return $"{Prefix}{protector.Protect(secret)}";
    }

    public string Unprotect(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);

        var value = protectedSecret.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedSecret[Prefix.Length..]
            : protectedSecret;

        return protector.Unprotect(value);
    }
}
