using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberRolePermissionTests
{
    [Test]
    [Arguments(ExternalMemberCustomerRole.GeneralUser, ExternalMemberPermissions.PaidAnalysisRequest, true)]
    [Arguments(ExternalMemberCustomerRole.GeneralUser, ExternalMemberPermissions.PaidAnalysisApprove, false)]
    [Arguments(ExternalMemberCustomerRole.PowerUser, ExternalMemberPermissions.PaidAnalysisApprove, true)]
    [Arguments(ExternalMemberCustomerRole.CustomerAdmin, ExternalMemberPermissions.MemberUsersManage, true)]
    [Arguments((ExternalMemberCustomerRole)999, ExternalMemberPermissions.PaidAnalysisRequest, false)]
    public async Task Roles_are_mapped_server_side_and_unknown_values_fail_closed(ExternalMemberCustomerRole role, string permission, bool expected)
    {
        await Assert.That(ExternalMemberPermissions.Allows(role, permission)).IsEqualTo(expected);
    }
}
