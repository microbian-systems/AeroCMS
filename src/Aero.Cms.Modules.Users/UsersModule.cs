using Aero.Cms.Core;
using Aero.Cms.Modules.Users.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Users;

/// <summary>
/// Registers administrative user, site-assignment, password, avatar, and current-profile endpoints.
/// </summary>
[Module(nameof(UsersModule))]
public sealed class UsersModule : AeroWebModule
{
    /// <inheritdoc />
public override string Name => nameof(UsersModule);

    /// <inheritdoc />
public override string Version => AeroConstants.Version;

    /// <inheritdoc />
public override string Author => AeroConstants.Author;

    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];

    /// <inheritdoc />
public override IReadOnlyList<string> Category => ["admin", "users"];

    /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["admin", "users", "profile", "management"];

    /// <summary>
    /// Maps user-management and current-profile endpoint groups during module startup.
    /// </summary>
    /// <param name="builder">The host endpoint route builder.</param>
    /// <returns>A completed task after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapUsersApi();
        builder.MapProfileApi();
        return Task.CompletedTask;
    }
}
