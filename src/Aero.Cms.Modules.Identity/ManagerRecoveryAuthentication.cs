using System.Security.Claims;
using Aero.Cms.Core;
using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Models.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Identity;

/// <summary>Defines the isolated manager-recovery authentication boundary.</summary>
public static class ManagerRecoveryDefaults
{
    /// <summary>The policy scheme that selects only a manager authentication cookie.</summary>
    public const string ManagerScheme = "AeroCms.Manager";

    /// <summary>The default authorization policy for manager UI and APIs.</summary>
    public const string ManagerPolicy = "AeroManager";

    /// <summary>The dedicated manager-recovery authentication scheme.</summary>
    public const string Scheme = "AeroCms.ManagerRecovery";

    /// <summary>The dedicated manager-recovery cookie name.</summary>
    public const string CookieName = ".AeroCms.ManagerRecovery";

    /// <summary>The named per-client recovery limiter policy.</summary>
    public const string RateLimitPolicy = "ManagerRecovery";

    /// <summary>The claim marking the one setup-owned recovery administrator.</summary>
    public const string AdministratorClaimType = "AeroCms.RecoveryAdministrator";

    /// <summary>The exact marker-claim value.</summary>
    public const string AdministratorClaimValue = "true";

    /// <summary>The maximum lifetime of a recovery session.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Configures manager authentication routing between normal and recovery sessions.
/// </summary>
public static class ManagerAuthenticationSchemeRouting
{
    /// <summary>
    /// Selects the recovery handler only for authentication requests carrying only its cookie,
    /// while always challenging through the normal manager application cookie.
    /// </summary>
    public static void Configure(PolicySchemeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ForwardDefaultSelector = context =>
            context.Request.Cookies.ContainsKey(ManagerRecoveryDefaults.CookieName)
            && !context.Request.Cookies.ContainsKey(".AeroCms.Auth")
                ? ManagerRecoveryDefaults.Scheme
                : IdentityConstants.ApplicationScheme;
        options.ForwardChallenge = IdentityConstants.ApplicationScheme;
    }
}

/// <summary>Records a durable manager-recovery authentication attempt.</summary>
/// <remarks>The submitted identifier and password are intentionally never persisted.</remarks>
public sealed class ManagerRecoverySecurityAudit : SableDocument
{
    /// <summary>Gets or sets when the attempt occurred.</summary>
    public DateTimeOffset AttemptedAtUtc { get; set; }

    /// <summary>Gets or sets the resolved recovery administrator, when validation reached that account.</summary>
    public long? RecoveryAdministratorUserId { get; set; }

    /// <summary>Gets or sets whether all recovery checks succeeded.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Gets or sets a non-sensitive internal outcome code.</summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Gets or sets the server-observed remote address.</summary>
    public string? RemoteAddress { get; set; }
}

/// <summary>Contains the result of a recovery credential check after its audit is durable.</summary>
public sealed record ManagerRecoveryAuthenticationResult(bool Succeeded, ClaimsPrincipal? Principal)
{
    /// <summary>Creates a failed result.</summary>
    public static ManagerRecoveryAuthenticationResult Failure { get; } = new(false, null);
}

/// <summary>Validates only the setup-marked recovery administrator and persists its security audit.</summary>
public interface IManagerRecoveryAuthenticationService
{
    /// <summary>Validates credentials and returns a recovery-only principal after audit persistence.</summary>
    Task<ManagerRecoveryAuthenticationResult> AuthenticateAsync(
        string? identifier,
        string? password,
        string? remoteAddress,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ManagerRecoveryAuthenticationService(
    UserManager<AeroUser> userManager,
    SignInManager<AeroUser> signInManager,
    IRecoveryAdministratorAuthority recoveryAdministratorAuthority,
    IDocumentSession session,
    TimeProvider timeProvider,
    ILogger<ManagerRecoveryAuthenticationService> logger) : IManagerRecoveryAuthenticationService
{
    /// <inheritdoc />
    public async Task<ManagerRecoveryAuthenticationResult> AuthenticateAsync(
        string? identifier,
        string? password,
        string? remoteAddress,
        CancellationToken cancellationToken = default)
    {
        AeroUser? user = null;
        var succeeded = false;
        var outcome = "rejected";

        var authoritativeUserId = await recoveryAdministratorAuthority.GetUserIdAsync(cancellationToken);
        var normalizedIdentifier = identifier?.Trim();
        if (authoritativeUserId is > 0
            && !string.IsNullOrWhiteSpace(normalizedIdentifier)
            && !string.IsNullOrWhiteSpace(password))
        {
            user = await userManager.FindByIdAsync(authoritativeUserId.Value.ToString());

            if (user is not null
                && user.Id == authoritativeUserId.Value
                && IdentifierMatches(user, normalizedIdentifier)
                && user.IsActive
                && !user.IsDeleted
                && await IsRecoveryAdministratorAsync(user)
                && await userManager.IsInRoleAsync(user, CmsRoleNames.Admin))
            {
                var passwordResult = await signInManager.CheckPasswordSignInAsync(
                    user,
                    password,
                    lockoutOnFailure: true);
                succeeded = passwordResult.Succeeded;
                outcome = succeeded ? "succeeded" : "rejected";
            }
        }

        var audit = new ManagerRecoverySecurityAudit
        {
            Id = Snowflake.NewId(),
            AttemptedAtUtc = timeProvider.GetUtcNow(),
            RecoveryAdministratorUserId = succeeded ? user!.Id : null,
            Succeeded = succeeded,
            Outcome = outcome,
            RemoteAddress = string.IsNullOrWhiteSpace(remoteAddress) ? null : remoteAddress
        };

        session.Store(audit);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Manager recovery authentication completed with outcome {Outcome}; audit {AuditId} is durable.",
            outcome,
            audit.Id);

        if (!succeeded)
        {
            return ManagerRecoveryAuthenticationResult.Failure;
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user!.Id.ToString()),
                new Claim("user_id", user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? "recovery-administrator"),
                new Claim(ClaimTypes.Role, CmsRoleNames.Admin),
                new Claim("is_admin", "true"),
                new Claim(ManagerRecoveryDefaults.AdministratorClaimType, ManagerRecoveryDefaults.AdministratorClaimValue)
            ],
            ManagerRecoveryDefaults.Scheme,
            ClaimTypes.Name,
            ClaimTypes.Role);

        return new ManagerRecoveryAuthenticationResult(true, new ClaimsPrincipal(identity));
    }

    private async Task<bool> IsRecoveryAdministratorAsync(AeroUser user)
    {
        var claims = await userManager.GetClaimsAsync(user);
        return claims.Any(claim =>
            string.Equals(claim.Type, ManagerRecoveryDefaults.AdministratorClaimType, StringComparison.Ordinal)
            && string.Equals(claim.Value, ManagerRecoveryDefaults.AdministratorClaimValue, StringComparison.Ordinal));
    }

    private static bool IdentifierMatches(AeroUser user, string identifier)
        => string.Equals(user.UserName, identifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Email, identifier, StringComparison.OrdinalIgnoreCase);
}
