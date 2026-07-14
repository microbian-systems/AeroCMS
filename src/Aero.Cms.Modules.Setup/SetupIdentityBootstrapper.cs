using Aero.Cms.Core;
using Aero.Core;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Represents a record for SetupIdentityBootstrapRequest.
/// </summary>
public sealed record SetupIdentityBootstrapRequest(
    string AdminUserName,
    string AdminEmail,
    string Password);

/// <summary>
/// Represents a class for SetupIdentityBootstrapResult.
/// </summary>
public sealed class SetupIdentityBootstrapResult
{
        /// <summary>
    /// Gets or sets the Succeeded.
    /// </summary>
public bool Succeeded => Errors.Count == 0;
        /// <summary>
    /// Gets or sets the Created Admin.
    /// </summary>
public bool CreatedAdmin { get; init; }
        /// <summary>
    /// Gets or sets the Created Roles.
    /// </summary>
public bool CreatedRoles { get; init; }
        /// <summary>
    /// Gets or sets the Admin User.
    /// </summary>
public AeroUser? AdminUser { get; init; }
        /// <summary>
    /// Gets or sets the Errors.
    /// </summary>
public List<IdentityError> Errors { get; } = [];

        /// <summary>
    /// Failure method.
    /// </summary>
public static SetupIdentityBootstrapResult Failure(IEnumerable<IdentityError> errors)
    {
        var result = new SetupIdentityBootstrapResult();
        result.Errors.AddRange(errors);
        return result;
    }
}

/// <summary>
/// Defines an interface for ISetupIdentityBootstrapper.
/// </summary>
public interface ISetupIdentityBootstrapper
{
        /// <summary>
    /// BootstrapAsync method.
    /// </summary>
Task<SetupIdentityBootstrapResult> BootstrapAsync(SetupIdentityBootstrapRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the installation's initial administrator is assigned the CMS administrator role.
    /// </summary>
    Task<SetupIdentityBootstrapResult> EnsureInitialAdminRoleAsync(string adminEmail, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for SetupIdentityBootstrapper.
/// </summary>
public sealed class SetupIdentityBootstrapper(
    UserManager<AeroUser> userManager,
    RoleManager<AeroRole> roleManager) : ISetupIdentityBootstrapper
{
        /// <summary>
    /// BootstrapAsync method.
    /// </summary>
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
