using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// A authorization token created by a login provider.
/// </summary>
public class IdentityUserAuthToken : IdentityUserToken<string>
{
}