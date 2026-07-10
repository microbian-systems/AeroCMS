using Aero.Cms.Core;
using Aero.Cms.Modules.Users.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Users;

/// <summary>
/// Aero CMS Users module - provides user and profile management functionality.
/// </summary>
[Module(nameof(UsersModule))]
public sealed class UsersModule : AeroWebModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(UsersModule);

        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;

        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;

        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["admin", "users"];

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["admin", "users", "profile", "management"];

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapUsersApi();
        builder.MapProfileApi();
        return Task.CompletedTask;
    }
}
