using System.Security.Claims;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Identity;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberBearerClaimRejectionTests
{
    [Test]
    [Arguments(ClaimTypes.Role, "Admin")]
    [Arguments("is_admin", "true")]
    [Arguments("permission", "member.users.manage")]
    public async Task Privileged_claims_are_rejected_from_member_principal(string type, string value)
    {
        var principal = ExternalMemberPrincipal.Create(1, LocalExternalMemberAuthentication.Provider, 2, 1);
        principal.Identities.Single().AddClaim(new Claim(type, value));
        await Assert.That(ExternalMemberPrincipal.TryRead(principal, out _)).IsFalse();
    }

    [Test]
    public async Task Manager_jwt_shape_is_not_an_external_member_principal()
    {
        var manager = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Bearer"));
        await Assert.That(ExternalMemberPrincipal.TryRead(manager, out _)).IsFalse();
    }
}
