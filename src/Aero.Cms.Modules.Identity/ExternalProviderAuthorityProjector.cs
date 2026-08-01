using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Identity;

/// <summary>Projects only a fully canonical active external-authority binding.</summary>
internal static class ExternalProviderAuthorityProjector
{
    public static bool TryProject(
        ExternalOrganizationBinding? binding,
        long expectedTenantId,
        out ExternalProviderAuthority authority,
        bool requireActive = true)
    {
        authority = default!;
        if (binding is not { Id: > 0, VaultId: > 0 } ||
            requireActive && !binding.IsActive ||
            binding.TenantId != expectedTenantId ||
            !ExternalMemberProviders.IsSupported(binding.Provider) ||
            !ExternalIdentityAuthorityRules.IsCanonicalOrganizationId(binding.Provider, binding.OrganizationId) ||
            !ExternalIdentityAuthorityRules.IsCanonicalIssuer(binding.Provider, binding.OrganizationId, binding.Issuer) ||
            !ExternalIdentityAuthorityRules.IsCanonicalAuthority(binding.Provider, binding.OrganizationId, binding.Authority) ||
            !ExternalIdentityAuthorityRules.IsCanonicalVaultEnvironment(binding.VaultEnvironment) ||
            !string.Equals(binding.BindingKey,
                ExternalIdentityAuthorityService.Key(binding.Provider, binding.Issuer, binding.OrganizationId),
                StringComparison.Ordinal) ||
            !string.Equals(binding.CredentialPath,
                ExternalProviderSecretReference.CanonicalCredentialPath(expectedTenantId, binding.Provider),
                StringComparison.Ordinal))
        {
            return false;
        }

        authority = new(
            binding.Id,
            binding.TenantId,
            binding.Provider,
            binding.Issuer,
            binding.OrganizationId,
            binding.Authority,
            new(binding.VaultId, binding.VaultEnvironment, binding.TenantId,
                binding.Provider, binding.CredentialPath));
        return true;
    }
}
