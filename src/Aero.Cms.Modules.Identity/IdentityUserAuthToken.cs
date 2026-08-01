using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Identity;

/// <summary>
/// Represents an unused ASP.NET Core Identity authentication-token record whose
/// Identity user key is a <see cref="string"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IdentityUserToken{TKey}"/> stores a named authentication token for
/// a user and login provider. It is distinct from <see cref="IdentityUserLogin{TKey}"/>,
/// which represents an external-login association.
/// </para>
/// <para>
/// The current module registers <see cref="long"/>-keyed users and does not register
/// or reference this type, making it incompatible with the active Identity store
/// model. It should be excluded from the curated DocFX API reference until removed or
/// redesigned.
/// </para>
/// <para>
/// The inherited token value is sensitive. This type adds no encryption-at-rest,
/// revocation, expiration, or key-sharing behavior.
/// </para>
/// </remarks>
public class IdentityUserAuthToken : IdentityUserToken<string>
{
}
