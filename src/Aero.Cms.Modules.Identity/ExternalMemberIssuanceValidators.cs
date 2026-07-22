using Aero.Cms.Abstractions.Authentication;
using FluentValidation;

namespace Aero.Cms.Modules.Identity;

public sealed class CreateExternalMemberInvitationRequestValidator
    : AbstractValidator<CreateExternalMemberInvitationRequest>
{
    public CreateExternalMemberInvitationRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
        RuleFor(request => request.SiteId).GreaterThan(0);
        RuleFor(request => request.OrganizationBindingId).GreaterThan(0);
        RuleFor(request => request.Provider).Must(ExternalMemberIssuanceRules.IsCanonicalProvider)
            .WithMessage("Provider must be canonical lower_snake_case.");
        RuleFor(request => request.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(request => request.ExpiresAt)
            .Must(expiresAt => expiresAt > timeProvider.GetUtcNow() &&
                expiresAt <= timeProvider.GetUtcNow().AddDays(7))
            .WithMessage("Invitation expiry must be in the future and no more than seven days away.");
    }
}

public sealed class BeginExternalMemberSignInRequestValidator
    : AbstractValidator<BeginExternalMemberSignInRequest>
{
    public BeginExternalMemberSignInRequestValidator()
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
        RuleFor(request => request.SiteId).GreaterThan(0);
        RuleFor(request => request.OrganizationBindingId).GreaterThan(0);
        When(request => request.InvitationHandle is not null, () =>
            RuleFor(request => request.InvitationHandle)
                .Must(ExternalMemberIssuanceRules.IsOpaqueHandle)
                .WithMessage("Invitation handle is invalid."));
        RuleFor(request => request.Provider).Must(ExternalMemberIssuanceRules.IsCanonicalProvider)
            .WithMessage("Provider must be canonical lower_snake_case.");
        RuleFor(request => request.ReturnPath).Must(ExternalMemberIssuanceRules.IsSafeLocalReturnPath)
            .WithMessage("Return path must be a safe local absolute path.");
        RuleFor(request => request.ProtectedProviderCorrelation).Must(ExternalMemberIssuanceRules.IsProtectedProviderCorrelation)
            .WithMessage("Protected provider correlation is invalid.");
    }
}

public sealed class CompleteExternalMemberSignInRequestValidator
    : AbstractValidator<CompleteExternalMemberSignInRequest>
{
    public CompleteExternalMemberSignInRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(request => request.AuthenticationHandle)
            .Must(ExternalMemberIssuanceRules.IsOpaqueHandle)
            .WithMessage("Authentication handle is invalid.");
        RuleFor(request => request.TenantId).GreaterThan(0);
        RuleFor(request => request.SiteId).GreaterThan(0);
        RuleFor(request => request.Provider).Must(ExternalMemberIssuanceRules.IsCanonicalProvider)
            .WithMessage("Provider must be canonical lower_snake_case.");
        RuleFor(request => request.Identity).NotNull();
        When(request => request.Identity is not null, () =>
        {
            RuleFor(request => request.Identity.Provider)
                .Must(ExternalMemberIssuanceRules.IsCanonicalProvider)
                .WithMessage("Identity provider must be canonical lower_snake_case.");
            RuleFor(request => request.Identity.Issuer).Must(ExternalMemberIssuanceRules.IsExactHttpsIssuer)
                .WithMessage("Issuer must be an exact HTTPS URI.");
            RuleFor(request => request.Identity.Subject).Must(ExternalMemberIssuanceRules.IsExactOpaqueValue)
                .WithMessage("Subject must be a nonblank exact opaque value.");
            RuleFor(request => request.Identity.OrganizationId).Must(ExternalMemberIssuanceRules.IsExactOpaqueValue)
                .WithMessage("Organization must be a nonblank exact opaque value.");
            RuleFor(request => request.Identity.Email).MaximumLength(320).EmailAddress();
            RuleFor(request => request.Identity.DisplayName).MaximumLength(256);
            RuleFor(request => request.Identity.ProviderSessionReference).MaximumLength(512);
            RuleFor(request => request.Identity.ValidatedAt)
                .Must(value => value >= timeProvider.GetUtcNow().Subtract(TimeSpan.FromMinutes(5)) &&
                               value <= timeProvider.GetUtcNow().Add(TimeSpan.FromMinutes(1)))
                .WithMessage("Validated identity is outside the accepted freshness window.");
        });
    }
}

internal static class ExternalMemberIssuanceRules
{
    private static readonly System.Text.RegularExpressions.Regex ProviderPattern =
        new("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool IsCanonicalProvider(string? value) =>
        value is { Length: >= 2 and <= 64 } && ProviderPattern.IsMatch(value) &&
        ExternalMemberProviders.IsSupported(value);

    public static bool IsProtectedProviderCorrelation(string? value) =>
        value is { Length: > 0 and <= 2048 } && string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(char.IsControl);

    public static bool IsExactHttpsIssuer(string? value) =>
        value is { Length: > 0 and <= 2048 } &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Fragment) &&
        string.IsNullOrEmpty(uri.Query) &&
        !string.IsNullOrEmpty(uri.Host);

    public static bool IsExactOpaqueValue(string? value) =>
        value is { Length: > 0 and <= 512 } &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(char.IsControl);

    public static bool IsSafeLocalReturnPath(string? value) =>
        value is { Length: > 0 and <= 512 } &&
        value[0] == '/' &&
        (value.Length == 1 || value[1] != '/') &&
        !value.Contains('\\', StringComparison.Ordinal) &&
        !value.Any(char.IsControl) &&
        Uri.TryCreate(value, UriKind.Relative, out _);

    public static bool IsOpaqueHandle(string? value)
    {
        if (value is not { Length: >= 46 and <= 64 }) return false;
        var separator = value.IndexOf('.');
        if (separator <= 0 || separator != value.LastIndexOf('.')) return false;
        if (!long.TryParse(value.AsSpan(0, separator), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var id) || id <= 0)
            return false;
        if (!string.Equals(id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                value[..separator], StringComparison.Ordinal))
            return false;
        var encoded = value[(separator + 1)..];
        if (encoded.Length != 43) return false;
        try
        {
            var bytes = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(encoded);
            return bytes.Length == 32 && string.Equals(
                Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(bytes), encoded,
                StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
