using Aero.Cms.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Identity;

/// <summary>Provides one process-wide verifier produced by the configured local-member password hasher.</summary>
public sealed class LocalExternalMemberPasswordSentinel
{
    public LocalExternalMemberPasswordSentinel(IPasswordHasher<ExternalMemberLocalCredential> passwordHasher)
    {
        Credential = new ExternalMemberLocalCredential
        {
            Id = long.MaxValue,
            TenantId = long.MaxValue,
            ExternalMemberId = long.MaxValue,
            NormalizedEmail = "dummy@invalid.example",
            SecurityVersion = 1,
            IsActive = false
        };
        PasswordHash = passwordHasher.HashPassword(Credential, "not-a-user-password");
    }

    public ExternalMemberLocalCredential Credential { get; }
    public string PasswordHash { get; }
}
