using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.AspNetCore.WebUtilities;

namespace Aero.Cms.Modules.Identity;

public interface IManagerIdentityAuthorityService
{
    Task<Result<ManagerIdentityAuthorityResult, AeroError>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ManagerIdentityAuthorityResult, AeroError>> ConfigureAsync(
        ConfigureManagerIdentityAuthorityRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureManagerIdentityAuthorityRequestValidator
    : AbstractValidator<ConfigureManagerIdentityAuthorityRequest>
{
    public ConfigureManagerIdentityAuthorityRequestValidator()
    {
        RuleFor(value => value.Provider).Must(ManagerIdentityAuthorityRules.IsSupportedProvider);
        RuleFor(value => value.OrganizationId).Must((request, organizationId) =>
            ManagerIdentityAuthorityRules.IsCanonicalOrganization(request.Provider, organizationId));
        RuleFor(value => value.Authority).Must((request, authority) =>
            ManagerIdentityAuthorityRules.IsCanonicalAuthority(request.Provider, request.OrganizationId, authority));
        RuleFor(value => value.PublicOrigin).Must(ManagerIdentityAuthorityRules.IsCanonicalPublicOrigin);
        RuleFor(value => value.VaultId).GreaterThan(0);
        RuleFor(value => value.VaultEnvironment).Must(ManagerIdentityAuthorityRules.IsCanonicalVaultEnvironment);
    }
}

internal static class ManagerIdentityAuthorityRules
{
    internal const string WorkOsAuthority = "https://api.workos.com";

    public static bool IsSupportedProvider(string? provider) => ManagerIdentityProviders.IsSupported(provider);

    public static bool IsCanonicalOrganization(string? provider, string? organizationId)
    {
        if (provider == ManagerIdentityProviders.WorkOs)
            return IsOpaque(organizationId);

        return provider == ManagerIdentityProviders.EntraWorkforce &&
               organizationId is not null &&
               Guid.TryParseExact(organizationId, "D", out var tenantId) &&
               string.Equals(organizationId, tenantId.ToString("D").ToLowerInvariant(), StringComparison.Ordinal);
    }

    public static bool IsCanonicalAuthority(string? provider, string? organizationId, string? authority)
    {
        if (!IsCanonicalOrganization(provider, organizationId)) return false;
        var expected = CanonicalAuthority(provider!, organizationId!);
        return string.Equals(authority, expected, StringComparison.Ordinal);
    }

    public static string CanonicalAuthority(string provider, string organizationId) =>
        provider == ManagerIdentityProviders.WorkOs
            ? WorkOsAuthority
            : $"https://login.microsoftonline.com/{organizationId}/v2.0";

    public static string CanonicalIssuer(string provider, string organizationId) =>
        CanonicalAuthority(provider, organizationId);

    public static bool IsCanonicalVaultEnvironment(string? value) =>
        value is { Length: > 0 and <= 128 } &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    public static bool IsCanonicalPublicOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var origin) ||
            !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) ||
            !origin.IsDefaultPort || !string.IsNullOrEmpty(origin.UserInfo) ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) ||
            !string.Equals(origin.AbsolutePath, "/", StringComparison.Ordinal) ||
            Uri.CheckHostName(origin.Host) == UriHostNameType.Unknown)
            return false;

        return string.Equals(value, origin.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal);
    }

    public static bool IsOpaque(string? value) =>
        value is { Length: > 0 and <= 512 } &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        value.All(character => character is >= '!' and <= '~');
}

public sealed class ManagerIdentityAuthorityService(
    IDocumentSession session,
    IValidator<ConfigureManagerIdentityAuthorityRequest> validator,
    TimeProvider timeProvider,
    IManagerAuthenticationModeResolver modeResolver) : IManagerIdentityAuthorityService
{
    public async Task<Result<ManagerIdentityAuthorityResult, AeroError>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var binding = await session.Query<ManagerIdentityAuthorityBinding>()
                .FirstOrDefaultAsync(value =>
                    value.SingletonKey == ManagerIdentityAuthorityBinding.InstallationSingletonKey,
                    cancellationToken);
            return binding is not null &&
                   ManagerIdentityAuthorityProjector.TryProject(binding, requireActive: false, out _)
                ? Prelude.Ok<ManagerIdentityAuthorityResult, AeroError>(Project(binding))
                : Prelude.Fail<ManagerIdentityAuthorityResult, AeroError>(
                    AeroError.NotFoundError("Manager identity authority is not configured."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            return Prelude.Fail<ManagerIdentityAuthorityResult, AeroError>(
                AeroError.DatabaseError("Manager identity authority could not be loaded."));
        }
    }

    public async Task<Result<ManagerIdentityAuthorityResult, AeroError>> ConfigureAsync(
        ConfigureManagerIdentityAuthorityRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Prelude.Fail<ManagerIdentityAuthorityResult, AeroError>(
                AeroError.ValidationError(validation.Errors.Select(error => error.ErrorMessage)));

        var modeResult = await modeResolver.ResolveAsync(cancellationToken);
        if (modeResult is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode) ||
            mode.Status != ManagerAuthenticationModeStatuses.Pending ||
            !string.Equals(mode.RequestedProvider, request.Provider, StringComparison.Ordinal))
            return Prelude.Fail<ManagerIdentityAuthorityResult, AeroError>(
                AeroError.ConflictError("The requested manager provider does not match setup intent."));

        try
        {
            var issuer = ManagerIdentityAuthorityRules.CanonicalIssuer(request.Provider, request.OrganizationId);
            var existing = await session.Query<ManagerIdentityAuthorityBinding>()
                .FirstOrDefaultAsync(binding =>
                    binding.SingletonKey == ManagerIdentityAuthorityBinding.InstallationSingletonKey,
                    cancellationToken);

            if (existing is not null &&
                (!string.Equals(existing.Provider, request.Provider, StringComparison.Ordinal) ||
                 !string.Equals(existing.OrganizationId, request.OrganizationId, StringComparison.Ordinal) ||
                 !string.Equals(existing.Issuer, issuer, StringComparison.Ordinal)))
                return Prelude.Fail<ManagerIdentityAuthorityResult, AeroError>(
                    AeroError.ConflictError("Manager identity authority is immutable once configured."));

            var expectedBindingKey = Key(request.Provider, issuer, request.OrganizationId);
            var expectedCredentialPath = ManagerProviderSecretReference.CanonicalCredentialPath(request.Provider);
            if (existing is { IsActive: true } or { IsVerified: true })
            {
                var exactExistingConfiguration =
                    string.Equals(existing.SingletonKey,
                        ManagerIdentityAuthorityBinding.InstallationSingletonKey, StringComparison.Ordinal) &&
                    string.Equals(existing.Provider, request.Provider, StringComparison.Ordinal) &&
                    string.Equals(existing.Issuer, issuer, StringComparison.Ordinal) &&
                    string.Equals(existing.OrganizationId, request.OrganizationId, StringComparison.Ordinal) &&
                    string.Equals(existing.Authority, request.Authority, StringComparison.Ordinal) &&
                    string.Equals(existing.PublicOrigin, request.PublicOrigin, StringComparison.Ordinal) &&
                    string.Equals(existing.BindingKey, expectedBindingKey, StringComparison.Ordinal) &&
                    existing.VaultId == request.VaultId &&
                    string.Equals(existing.VaultEnvironment, request.VaultEnvironment, StringComparison.Ordinal) &&
                    string.Equals(existing.CredentialPath, expectedCredentialPath, StringComparison.Ordinal);
                return exactExistingConfiguration
                    ? Prelude.Ok<ManagerIdentityAuthorityResult, AeroError>(Project(existing))
                    : Prelude.Fail<ManagerIdentityAuthorityResult, AeroError>(
                        AeroError.ConflictError("An active manager identity authority cannot be changed."));
            }

            var binding = existing ?? new ManagerIdentityAuthorityBinding
            {
                Id = Snowflake.NewId(),
                SingletonKey = ManagerIdentityAuthorityBinding.InstallationSingletonKey,
                Provider = request.Provider,
                OrganizationId = request.OrganizationId,
                Issuer = issuer,
                BindingKey = expectedBindingKey,
                CreatedOn = timeProvider.GetUtcNow(),
                IsActive = false,
                IsVerified = false
            };

            binding.Authority = request.Authority;
            binding.PublicOrigin = request.PublicOrigin;
            binding.VaultId = request.VaultId;
            binding.VaultEnvironment = request.VaultEnvironment;
            binding.CredentialPath = expectedCredentialPath;
            if (existing is not null) binding.ModifiedOn = timeProvider.GetUtcNow();

            session.Store(binding);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<ManagerIdentityAuthorityResult, AeroError>(Project(binding));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("concurr", StringComparison.OrdinalIgnoreCase))
        {
            session.ClearChanges();
            return Prelude.Fail<ManagerIdentityAuthorityResult, AeroError>(
                AeroError.ConflictError("Manager identity authority conflicts with the existing binding."));
        }
        catch
        {
            session.ClearChanges();
            return Prelude.Fail<ManagerIdentityAuthorityResult, AeroError>(
                AeroError.DatabaseError("Manager identity authority could not be configured."));
        }
    }

    internal static ManagerIdentityAuthorityResult Project(ManagerIdentityAuthorityBinding binding) =>
        new(binding.Id, binding.Provider, binding.Issuer, binding.OrganizationId, binding.Authority,
            binding.PublicOrigin, binding.VaultId, binding.VaultEnvironment, binding.IsVerified, binding.IsActive);

    internal static string Key(params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return "v1." + WebEncoders.Base64UrlEncode(hash.GetHashAndReset());
    }
}
