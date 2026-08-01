using Aero.Cms.Abstractions.Authentication;
using FluentValidation;

namespace Aero.Cms.Modules.Identity;

public sealed class CreateLocalExternalMemberInvitationRequestValidator
    : AbstractValidator<CreateLocalExternalMemberInvitationRequest>
{
    public CreateLocalExternalMemberInvitationRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
        RuleFor(request => request.SiteId).GreaterThan(0);
        RuleFor(request => request.LocalAuthorityId).GreaterThan(0);
        RuleFor(request => request.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(request => request.ExpiresAt)
            .Must(value => value > timeProvider.GetUtcNow() && value <= timeProvider.GetUtcNow().AddDays(7))
            .WithMessage("Invitation expiry must be in the future and no more than seven days away.");
    }
}

public sealed class ActivateLocalExternalMemberInvitationRequestValidator
    : AbstractValidator<ActivateLocalExternalMemberInvitationRequest>
{
    public ActivateLocalExternalMemberInvitationRequestValidator()
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
        RuleFor(request => request.SiteId).GreaterThan(0);
        RuleFor(request => request.InvitationHandle).Must(ExternalMemberIssuanceRules.IsOpaqueHandle)
            .WithMessage("Invitation handle is invalid.");
        RuleFor(request => request.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(request => request.Password).NotEmpty().MinimumLength(12).MaximumLength(256);
        RuleFor(request => request.DisplayName).MaximumLength(256);
        RuleFor(request => request.ReturnPath).Must(ExternalMemberIssuanceRules.IsSafeLocalReturnPath)
            .WithMessage("Return path must be a safe local absolute path.");
    }
}

public sealed class LoginLocalExternalMemberRequestValidator
    : AbstractValidator<LoginLocalExternalMemberRequest>
{
    public LoginLocalExternalMemberRequestValidator()
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
        RuleFor(request => request.SiteId).GreaterThan(0);
        RuleFor(request => request.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(request => request.Password).NotEmpty().MaximumLength(256);
        RuleFor(request => request.ReturnPath).Must(ExternalMemberIssuanceRules.IsSafeLocalReturnPath)
            .WithMessage("Return path must be a safe local absolute path.");
    }
}

public sealed class ResetLocalExternalMemberPasswordRequestValidator
    : AbstractValidator<ResetLocalExternalMemberPasswordRequest>
{
    public ResetLocalExternalMemberPasswordRequestValidator()
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
        RuleFor(request => request.SiteId).GreaterThan(0);
        RuleFor(request => request.ResetHandle).Must(ExternalMemberIssuanceRules.IsOpaqueHandle)
            .WithMessage("Reset handle is invalid.");
        RuleFor(request => request.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(256);
        RuleFor(request => request.ReturnPath).Must(ExternalMemberIssuanceRules.IsSafeLocalReturnPath)
            .WithMessage("Return path must be a safe local absolute path.");
    }
}

public sealed class IssueLocalExternalMemberPasswordResetRequestValidator
    : AbstractValidator<IssueLocalExternalMemberPasswordResetRequest>
{
    public IssueLocalExternalMemberPasswordResetRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
        RuleFor(request => request.SiteId).GreaterThan(0);
        RuleFor(request => request.ExternalMemberId).GreaterThan(0);
        RuleFor(request => request.IssuedByManagerUserId).GreaterThan(0);
        RuleFor(request => request.ExpiresAt)
            .Must(value => value > timeProvider.GetUtcNow() && value <= timeProvider.GetUtcNow().AddDays(1))
            .WithMessage("Password-reset expiry must be in the future and no more than one day away.");
    }
}
