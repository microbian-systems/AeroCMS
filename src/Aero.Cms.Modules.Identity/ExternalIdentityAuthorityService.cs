using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Core.Http;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Aero.Cms.Modules.Identity;

public sealed record ExternalIdentityManagerScope(long TenantId, long SiteId);

/// <summary>Resolves the persisted, enabled manager site scope for an external authority change.</summary>
public interface IExternalIdentityManagerScopeResolver
{
    Task<Result<ExternalIdentityManagerScope, AeroError>> ResolveAsync(CancellationToken ct = default);
}
public sealed class ExternalIdentityManagerScopeResolver(ISiteContext siteContext, IHttpContextAccessor accessor, IDocumentSession session) : IExternalIdentityManagerScopeResolver
{
    public async Task<Result<ExternalIdentityManagerScope, AeroError>> ResolveAsync(CancellationToken ct = default)
    {
        var cookie = accessor.HttpContext?.Request.Cookies["AeroCms.SiteId"];
        if (!long.TryParse(cookie, out var id) || id <= 0 || siteContext.SiteId != id)
            return Prelude.Fail<ExternalIdentityManagerScope, AeroError>(AeroError.NotFoundError("Site not found."));
        try
        {
            var site = await session.Query<SitesModel>().FirstOrDefaultAsync(x => x.Id == id, ct);
            return site is { TenantId: > 0, IsEnabled: true }
                ? Prelude.Ok<ExternalIdentityManagerScope, AeroError>(new(site.TenantId, site.Id))
                : Prelude.Fail<ExternalIdentityManagerScope, AeroError>(AeroError.NotFoundError("Site not found."));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Prelude.Fail<ExternalIdentityManagerScope, AeroError>(AeroError.DatabaseError("Site scope could not be resolved."));
        }
    }
}

/// <summary>Non-secret projection of the configured external authority.</summary>
public sealed record ExternalIdentityAuthorityResult(
    long BindingId,
    long TenantId,
    string Provider,
    string Issuer,
    string OrganizationId,
    string Authority,
    long VaultId,
    string VaultEnvironment,
    bool Enabled);

/// <summary>Creates or updates the non-secret authority binding for one tenant.</summary>
public interface IExternalIdentityAuthorityService
{
    Task<Result<ExternalIdentityAuthorityResult, AeroError>> ConfigureAsync(
        ExternalIdentityManagerScope scope,
        ConfigureExternalIdentityAuthorityRequest request,
        CancellationToken ct = default);
}
public sealed class ConfigureExternalIdentityAuthorityRequestValidator : AbstractValidator<ConfigureExternalIdentityAuthorityRequest>
{
    public ConfigureExternalIdentityAuthorityRequestValidator()
    {
        RuleFor(x => x.Provider).Must(ExternalMemberProviders.IsSupported);
        RuleFor(x => x.OrganizationId)
            .Must((request, value) => ExternalIdentityAuthorityRules.IsCanonicalOrganizationId(request.Provider, value));
        RuleFor(x => x.VaultId).GreaterThan(0);
        RuleFor(x => x.VaultEnvironment).Must(ExternalIdentityAuthorityRules.IsCanonicalVaultEnvironment);
        RuleFor(x => x.Authority).Must((request, value) =>
            request.Provider == ExternalMemberProviders.WorkOs ||
            ExternalIdentityAuthorityRules.IsCanonicalAuthority(request.Provider, request.OrganizationId, value));
        RuleFor(x => x.AdditionalProperties).Must(value => value is null || value.Count == 0);
    }
}
internal static class ExternalIdentityAuthorityRules
{
    private static readonly Regex EntraTenantLabelPattern = new(
        "^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static bool IsCanonicalVaultEnvironment(string? value) =>
        value is { Length: > 0 and <= 128 } &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');

    public static bool IsCanonicalOrganizationId(string? provider, string? value) =>
        provider == ExternalMemberProviders.WorkOs
            ? ExternalMemberIssuanceRules.IsExactOpaqueValue(value)
            : provider == ExternalMemberProviders.EntraExternalId &&
              value is not null &&
              Guid.TryParseExact(value, "D", out var parsed) &&
              string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal);

    public static bool IsCanonicalAuthority(string? provider, string? organizationId, string? value) =>
        IsCanonicalUri(provider, organizationId, value, isAuthority: true);

    public static bool IsCanonicalIssuer(string? provider, string? organizationId, string? value) =>
        IsCanonicalUri(provider, organizationId, value, isAuthority: false);

    public static string CanonicalEntraIssuer(string organizationId) =>
        $"https://{organizationId}.ciamlogin.com/{organizationId}/v2.0";

    private static bool IsCanonicalUri(string? provider, string? organizationId, string? value, bool isAuthority)
    {
        if (!IsCanonicalOrganizationId(provider, organizationId) ||
            !ExternalMemberIssuanceRules.IsExactHttpsIssuer(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !uri.IsDefaultPort)
            return false;

        if (provider == ExternalMemberProviders.WorkOs)
            return string.Equals(value, "https://api.workos.com", StringComparison.Ordinal);

        if (provider != ExternalMemberProviders.EntraExternalId)
            return false;

        var organization = CanonicalOrganizationId(organizationId!);
        if (!uri.Host.EndsWith(".ciamlogin.com", StringComparison.Ordinal))
            return false;
        var authorityHostLabel = isAuthority ? uri.Host[..^".ciamlogin.com".Length] : organization;
        if (!EntraTenantLabelPattern.IsMatch(authorityHostLabel) ||
            authorityHostLabel.StartsWith("xn--", StringComparison.Ordinal) ||
            !string.Equals(uri.IdnHost, uri.Host, StringComparison.Ordinal))
            return false;

        var expectedHost = $"{authorityHostLabel}.ciamlogin.com";
        var expectedPath = $"/{Uri.EscapeDataString(organization)}/v2.0";
        return string.Equals(uri.Host, expectedHost, StringComparison.Ordinal) &&
            string.Equals(uri.AbsolutePath, expectedPath, StringComparison.Ordinal) &&
            string.Equals(value, $"https://{expectedHost}{expectedPath}", StringComparison.Ordinal);
    }

    private static string CanonicalOrganizationId(string organizationId) =>
        Guid.TryParse(organizationId, out var parsed) ? parsed.ToString("D").ToLowerInvariant() : organizationId;
}
public sealed class ExternalIdentityAuthorityService(IDocumentSession session, IValidator<ConfigureExternalIdentityAuthorityRequest> validator, TimeProvider time) : IExternalIdentityAuthorityService
{
    public async Task<Result<ExternalIdentityAuthorityResult, AeroError>> ConfigureAsync(
        ExternalIdentityManagerScope scope,
        ConfigureExternalIdentityAuthorityRequest request,
        CancellationToken ct = default)
    {
        if (scope.TenantId <= 0 || scope.SiteId <= 0)
            return Fail();

        var valid = await validator.ValidateAsync(request, ct);
        if (!valid.IsValid)
            return Prelude.Fail<ExternalIdentityAuthorityResult, AeroError>(
                AeroError.ValidationError(valid.Errors.Select(x => x.ErrorMessage)));

        try
        {
            var activeLocalAuthorities = await session.Query<ExternalMemberLocalAuthority>()
                .Where(authority => authority.TenantId == scope.TenantId && authority.IsActive)
                .ToListAsync(ct);
            if (activeLocalAuthorities.Count != 0)
                return Prelude.Fail<ExternalIdentityAuthorityResult, AeroError>(
                    AeroError.ConflictError("External authority conflicts with the active local authority."));

            var issuer = request.Provider == ExternalMemberProviders.EntraExternalId
                ? ExternalIdentityAuthorityRules.CanonicalEntraIssuer(request.OrganizationId)
                : "https://api.workos.com";
            var authority = request.Provider == ExternalMemberProviders.WorkOs
                ? "https://api.workos.com"
                : request.Authority;
            var existing = await session.Query<ExternalOrganizationBinding>().FirstOrDefaultAsync(x => x.TenantId == scope.TenantId, ct);
            if (existing is not null &&
                (!string.Equals(existing.Provider, request.Provider, StringComparison.Ordinal) ||
                 !string.Equals(existing.Issuer, issuer, StringComparison.Ordinal) ||
                 !string.Equals(existing.OrganizationId, request.OrganizationId, StringComparison.Ordinal)))
                return Prelude.Fail<ExternalIdentityAuthorityResult, AeroError>(
                    AeroError.ConflictError("External authority conflicts with the existing binding."));

            var binding = existing ?? new ExternalOrganizationBinding
            {
                Id = Snowflake.NewId(),
                TenantId = scope.TenantId,
                Provider = request.Provider,
                Issuer = issuer,
                OrganizationId = request.OrganizationId,
                BindingKey = Key(request.Provider, issuer, request.OrganizationId),
                CreatedOn = time.GetUtcNow()
            };
            binding.Authority = authority;
            binding.VaultId = request.VaultId;
            binding.VaultEnvironment = request.VaultEnvironment;
            binding.CredentialPath = ExternalProviderSecretReference.CanonicalCredentialPath(scope.TenantId, request.Provider);
            binding.IsActive = request.Enabled;
            binding.ModifiedOn = existing is null ? null : time.GetUtcNow();
            session.Store(binding);
            await session.SaveChangesAsync(ct);

            var b = binding;
            return Prelude.Ok<ExternalIdentityAuthorityResult, AeroError>(new(b.Id, b.TenantId, b.Provider, b.Issuer, b.OrganizationId, b.Authority, b.VaultId, b.VaultEnvironment, b.IsActive));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("concurr", StringComparison.OrdinalIgnoreCase))
        {
            session.ClearChanges();
            return Prelude.Fail<ExternalIdentityAuthorityResult, AeroError>(
                AeroError.ConflictError("External authority conflicts with the existing binding."));
        }
        catch
        {
            session.ClearChanges();
            return Fail();
        }
    }

    internal static string Key(params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return "v1." + WebEncoders.Base64UrlEncode(hash.GetHashAndReset());
    }

    private static Result<ExternalIdentityAuthorityResult, AeroError> Fail() =>
        Prelude.Fail<ExternalIdentityAuthorityResult, AeroError>(
            AeroError.DatabaseError("External authority could not be configured."));
}
