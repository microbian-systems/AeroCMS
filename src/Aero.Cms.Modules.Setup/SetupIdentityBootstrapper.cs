using Aero.Cms.Core;
using Aero.Core;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Contains the credentials used to create the installation's initial administrator.
/// </summary>
/// <remarks><see cref="Password"/> is sensitive and must not be logged or persisted by callers.</remarks>
public sealed record SetupIdentityBootstrapRequest(
    string AdminUserName,
    string AdminEmail,
    string Password);

/// <summary>
/// Describes identity artifacts created or verified during setup.
/// </summary>
public sealed class SetupIdentityBootstrapResult
{
    /// <summary>
    /// Gets whether the operation produced no Identity errors.
    /// </summary>
public bool Succeeded => Errors.Count == 0;
    /// <summary>
    /// Gets whether a new administrator account was created.
    /// </summary>
public bool CreatedAdmin { get; init; }
    /// <summary>
    /// Gets whether any CMS role or the administrator's role assignment was created.
    /// </summary>
public bool CreatedRoles { get; init; }
    /// <summary>
    /// Gets the administrator account that was created or selected.
    /// </summary>
public AeroUser? AdminUser { get; init; }
    /// <summary>
    /// Gets Identity errors returned by role, user, or membership operations.
    /// </summary>
public List<IdentityError> Errors { get; } = [];

    /// <summary>
    /// Creates a failed result from Identity errors.
    /// </summary>
    /// <param name="errors">The errors to copy into the result.</param>
    /// <returns>A result whose <see cref="Succeeded"/> value is <see langword="false"/> when at least one error is supplied.</returns>
public static SetupIdentityBootstrapResult Failure(IEnumerable<IdentityError> errors)
    {
        var result = new SetupIdentityBootstrapResult();
        result.Errors.AddRange(errors);
        return result;
    }
}

/// <summary>
/// Creates or repairs the initial CMS administrator and required CMS roles.
/// </summary>
public interface ISetupIdentityBootstrapper
{
    /// <summary>
    /// Ensures CMS roles exist, selects or creates an administrator, and assigns the administrator role.
    /// </summary>
    /// <param name="request">The initial administrator identity and password.</param>
    /// <param name="cancellationToken">Accepted for workflow coordination; current Identity manager calls do not consume it.</param>
    /// <returns>A result containing created-state flags, the selected administrator, or Identity errors.</returns>
Task<SetupIdentityBootstrapResult> BootstrapAsync(SetupIdentityBootstrapRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the installation's initial administrator is assigned the CMS administrator role.
    /// </summary>
    /// <param name="adminEmail">The persisted setup administrator email.</param>
    /// <param name="cancellationToken">Accepted for workflow coordination; current Identity manager calls do not consume it.</param>
    /// <returns>A result describing role creation or assignment and any Identity errors.</returns>
    Task<SetupIdentityBootstrapResult> EnsureInitialAdminRoleAsync(string adminEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the exact setup recovery administrator remains active, marked, and assigned Admin.
    /// </summary>
    Task<SetupIdentityBootstrapResult> EnsureRecoveryAdministratorAsync(
        long? recoveryAdministratorUserId,
        string adminEmail,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements idempotent role provisioning and initial-administrator selection with ASP.NET Core Identity managers.
/// </summary>
/// <remarks>
/// If any user already belongs to the CMS administrator role, that user is retained and the
/// requested email is not used to create another administrator. Partial role creation is not
/// rolled back if a later Identity operation fails.
/// </remarks>
public sealed class SetupIdentityBootstrapper(
    UserManager<AeroUser> userManager,
    RoleManager<AeroRole> roleManager) : ISetupIdentityBootstrapper
{
    private const string RecoveryAdministratorClaimType = "AeroCms.RecoveryAdministrator";
    private const string RecoveryAdministratorClaimValue = "true";

    /// <inheritdoc />
public async Task<SetupIdentityBootstrapResult> BootstrapAsync(SetupIdentityBootstrapRequest request, CancellationToken cancellationToken = default)
    {
        var roleResult = await EnsureCmsRolesAsync(cancellationToken);
        if (!roleResult.Succeeded)
        {
            return SetupIdentityBootstrapResult.Failure(roleResult.Errors);
        }

        var existingAdmins = await userManager.GetUsersInRoleAsync(CmsRoleNames.Admin);
        var adminUser = existingAdmins.FirstOrDefault();
        var createdAdmin = false;
        var createdRoles = roleResult.CreatedRoles;

        if (adminUser == null)
        {
            adminUser = await userManager.FindByEmailAsync(request.AdminEmail);

            if (adminUser == null)
            {
                adminUser = new AeroUser
                {
                    Id = Snowflake.NewId(),
                    UserName = request.AdminUserName,
                    Email = request.AdminEmail,
                    EmailConfirmed = true,
                    IsActive = true
                };

                var createAdminResult = await userManager.CreateAsync(adminUser, request.Password);
                if (!createAdminResult.Succeeded)
                {
                    return SetupIdentityBootstrapResult.Failure(createAdminResult.Errors);
                }

                createdAdmin = true;
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, CmsRoleNames.Admin))
        {
            createdRoles = true;
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, CmsRoleNames.Admin);
            if (!addToRoleResult.Succeeded)
            {
                return SetupIdentityBootstrapResult.Failure(addToRoleResult.Errors);
            }
        }

        var recoveryMarkerResult = await CanonicalizeRecoveryMarkerAsync(adminUser);
        if (!recoveryMarkerResult.Succeeded)
        {
            return SetupIdentityBootstrapResult.Failure(recoveryMarkerResult.Errors);
        }

        return new SetupIdentityBootstrapResult
        {
            AdminUser = adminUser,
            CreatedAdmin = createdAdmin,
            CreatedRoles = createdRoles
        };
    }

    /// <inheritdoc />
    public async Task<SetupIdentityBootstrapResult> EnsureInitialAdminRoleAsync(
        string adminEmail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return SetupIdentityBootstrapResult.Failure([new IdentityError
            {
                Description = "The setup administrator email is required to repair CMS role membership."
            }]);
        }

        var roleResult = await EnsureCmsRolesAsync(cancellationToken);
        if (!roleResult.Succeeded)
        {
            return SetupIdentityBootstrapResult.Failure(roleResult.Errors);
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            return SetupIdentityBootstrapResult.Failure([new IdentityError
            {
                Description = "The setup administrator could not be found for CMS role repair."
            }]);
        }

        var addedAdminRole = false;
        if (!await userManager.IsInRoleAsync(adminUser, CmsRoleNames.Admin))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, CmsRoleNames.Admin);
            if (!addToRoleResult.Succeeded)
            {
                return SetupIdentityBootstrapResult.Failure(addToRoleResult.Errors);
            }

            addedAdminRole = true;
        }

        return new SetupIdentityBootstrapResult
        {
            AdminUser = adminUser,
            CreatedRoles = roleResult.CreatedRoles || addedAdminRole
        };
    }

    /// <inheritdoc />
    public async Task<SetupIdentityBootstrapResult> EnsureRecoveryAdministratorAsync(
        long? recoveryAdministratorUserId,
        string adminEmail,
        CancellationToken cancellationToken = default)
    {
        var roleResult = await EnsureCmsRolesAsync(cancellationToken);
        if (!roleResult.Succeeded)
        {
            return SetupIdentityBootstrapResult.Failure(roleResult.Errors);
        }

        var adminUser = recoveryAdministratorUserId is > 0
            ? await userManager.FindByIdAsync(recoveryAdministratorUserId.Value.ToString())
            : await userManager.FindByEmailAsync(adminEmail);

        if (adminUser is null)
        {
            return SetupIdentityBootstrapResult.Failure([new IdentityError
            {
                Description = "The setup recovery administrator could not be found."
            }]);
        }

        var changed = roleResult.CreatedRoles;
        if (!adminUser.IsActive || adminUser.IsDeleted)
        {
            adminUser.IsActive = true;
            adminUser.IsDeleted = false;
            adminUser.DeletedOn = null;
            var updateResult = await userManager.UpdateAsync(adminUser);
            if (!updateResult.Succeeded)
            {
                return SetupIdentityBootstrapResult.Failure(updateResult.Errors);
            }

            changed = true;
        }

        if (!await userManager.IsInRoleAsync(adminUser, CmsRoleNames.Admin))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, CmsRoleNames.Admin);
            if (!addToRoleResult.Succeeded)
            {
                return SetupIdentityBootstrapResult.Failure(addToRoleResult.Errors);
            }

            changed = true;
        }

        var markerResult = await CanonicalizeRecoveryMarkerAsync(adminUser);
        if (!markerResult.Succeeded)
        {
            return SetupIdentityBootstrapResult.Failure(markerResult.Errors);
        }

        return new SetupIdentityBootstrapResult
        {
            AdminUser = adminUser,
            CreatedRoles = changed
        };
    }

    private async Task<IdentityResult> CanonicalizeRecoveryMarkerAsync(AeroUser adminUser)
    {
        var canonicalMarkerExists = false;
        foreach (var user in userManager.Users.ToList())
        {
            var claims = await userManager.GetClaimsAsync(user);
            foreach (var claim in claims.Where(claim =>
                         string.Equals(claim.Type, RecoveryAdministratorClaimType, StringComparison.Ordinal)))
            {
                var isCanonical = user.Id == adminUser.Id
                    && string.Equals(claim.Value, RecoveryAdministratorClaimValue, StringComparison.Ordinal)
                    && !canonicalMarkerExists;

                if (isCanonical)
                {
                    canonicalMarkerExists = true;
                    continue;
                }

                var removeResult = await userManager.RemoveClaimAsync(user, claim);
                if (!removeResult.Succeeded)
                {
                    return removeResult;
                }
            }
        }

        if (canonicalMarkerExists)
        {
            return IdentityResult.Success;
        }

        return await userManager.AddClaimAsync(
            adminUser,
            new Claim(RecoveryAdministratorClaimType, RecoveryAdministratorClaimValue));
    }

    /// <summary>
    /// Creates each missing CMS role and stops at the first Identity failure.
    /// </summary>
    private async Task<SetupIdentityBootstrapResult> EnsureCmsRolesAsync(CancellationToken cancellationToken)
    {
        var createdRoles = false;
        foreach (var roleName in CmsRoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var createResult = await roleManager.CreateAsync(new AeroRole
            {
                Id = Snowflake.NewId(),
                Name = roleName
            });

            if (!createResult.Succeeded)
            {
                return SetupIdentityBootstrapResult.Failure(createResult.Errors);
            }

            createdRoles = true;
        }

        return new SetupIdentityBootstrapResult { CreatedRoles = createdRoles };
    }
}
