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
    public override string Name => nameof(UsersModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["admin", "users"];

    public override IReadOnlyList<string> Tags => ["admin", "users", "profile", "management"];

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapUsersApi();
        builder.MapProfileApi();
        return Task.CompletedTask;
    }
}
